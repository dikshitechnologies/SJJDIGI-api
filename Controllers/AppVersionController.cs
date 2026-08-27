using JEWELLBISREACT.DBConnection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace CHITSCHEME.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AppVersionController : ControllerBase
    {
        // ─────────────────────────────────────────────────────────────────────
        // GET api/AppVersion/check?platform=android&versionCode=10
        //
        // Called by the app on every startup (and on resume).
        // Compares the installed versionCode against the latest in DB.
        // Never compare version strings — always use integer versionCode.
        //
        // Response when update available:
        // {
        //   "updateAvailable": true,
        //   "latestVersion":   "1.1.0",
        //   "latestVersionCode": 11,
        //   "mandatory":       false,
        //   "message":         "A new version is available...",
        //   "storeUrl":        "https://play.google.com/..."
        // }
        //
        // Response when already on latest:
        // {
        //   "updateAvailable":   false,
        //   "latestVersion":     "1.1.0",
        //   "latestVersionCode": 11
        // }
        // ─────────────────────────────────────────────────────────────────────
        [AllowAnonymous]
        [HttpGet("check")]
        public async Task<IActionResult> CheckVersion(
            [FromQuery] string platform,
            [FromQuery] int versionCode)
        {
            // ── Validate input ────────────────────────────────────────────────
            if (string.IsNullOrWhiteSpace(platform))
                return BadRequest(new { message = "platform is required. Use 'android' or 'ios'." });

            platform = platform.ToLowerInvariant().Trim();

            if (platform != "android" && platform != "ios")
                return BadRequest(new { message = "platform must be 'android' or 'ios'." });

            if (versionCode <= 0)
                return BadRequest(new { message = "versionCode must be a positive integer." });

            try
            {
                using var conn = new SqlConnection(DBHelper.GetConnection());
                await conn.OpenAsync();

                using var cmd = new SqlCommand(@"
                    SELECT TOP 1
                        Version,
                        VersionCode,
                        IsMandatory,
                        UpdateMessage,
                        StoreUrl
                    FROM dbo.AppVersion
                    WHERE Platform  = @platform
                      AND IsActive  = 1
                    ORDER BY VersionCode DESC",
                    conn);

                cmd.Parameters.AddWithValue("@platform", platform);

                using var reader = await cmd.ExecuteReaderAsync();

                if (!await reader.ReadAsync())
                {
                    // No active row for this platform — let the app through.
                    return Ok(new
                    {
                        updateAvailable = false,
                        message         = "No version info found for this platform."
                    });
                }

                string  latestVersion     = reader["Version"].ToString();
                int     latestVersionCode = Convert.ToInt32(reader["VersionCode"]);
                bool    isMandatory       = Convert.ToBoolean(reader["IsMandatory"]);
                string  updateMessage     = reader["UpdateMessage"] == DBNull.Value
                                                ? "A new version is available. Please update the app."
                                                : reader["UpdateMessage"].ToString();
                string  storeUrl          = reader["StoreUrl"] == DBNull.Value
                                                ? null
                                                : reader["StoreUrl"].ToString();

                // ── Core comparison: integer versionCode only ─────────────────
                // App versionCode < latest → update available
                // App versionCode >= latest → no update needed
                bool updateAvailable = versionCode < latestVersionCode;

                if (!updateAvailable)
                {
                    return Ok(new
                    {
                        updateAvailable   = false,
                        latestVersion     = latestVersion,
                        latestVersionCode = latestVersionCode
                    });
                }

                return Ok(new
                {
                    updateAvailable   = true,
                    latestVersion     = latestVersion,
                    latestVersionCode = latestVersionCode,
                    mandatory         = isMandatory,
                    message           = updateMessage,
                    storeUrl          = storeUrl
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Version check failed.", error = ex.Message });
            }
        }


        // ─────────────────────────────────────────────────────────────────────
        // POST api/AppVersion/update
        //
        // Admin endpoint — update the latest version in DB when a new build
        // is released on Play Store / App Store.
        //
        // Requires JWT auth (admin use only — do not call from the mobile app).
        // ─────────────────────────────────────────────────────────────────────
        [Authorize]
        [HttpPost("update")]
        public async Task<IActionResult> UpdateVersion([FromBody] UpdateVersionRequest req)
        {
            if (req == null
                || string.IsNullOrWhiteSpace(req.Platform)
                || string.IsNullOrWhiteSpace(req.Version)
                || req.VersionCode <= 0)
                return BadRequest(new { message = "Platform, Version, and VersionCode are required." });

            req.Platform = req.Platform.ToLowerInvariant().Trim();

            if (req.Platform != "android" && req.Platform != "ios")
                return BadRequest(new { message = "Platform must be 'android' or 'ios'." });

            try
            {
                using var conn = new SqlConnection(DBHelper.GetConnection());
                await conn.OpenAsync();

                // MERGE: update existing row, insert if not present
                using var cmd = new SqlCommand(@"
                    MERGE dbo.AppVersion AS target
                    USING (SELECT @platform AS Platform) AS src
                        ON target.Platform = src.Platform
                    WHEN MATCHED THEN
                        UPDATE SET
                            Version       = @version,
                            VersionCode   = @versionCode,
                            IsMandatory   = @mandatory,
                            UpdateMessage = @message,
                            StoreUrl      = @storeUrl,
                            IsActive      = 1,
                            UpdatedAt     = SYSUTCDATETIME()
                    WHEN NOT MATCHED THEN
                        INSERT (Platform, Version, VersionCode, IsMandatory,
                                UpdateMessage, StoreUrl, IsActive)
                        VALUES (@platform, @version, @versionCode, @mandatory,
                                @message, @storeUrl, 1);",
                    conn);

                cmd.Parameters.AddWithValue("@platform",    req.Platform);
                cmd.Parameters.AddWithValue("@version",     req.Version);
                cmd.Parameters.AddWithValue("@versionCode", req.VersionCode);
                cmd.Parameters.AddWithValue("@mandatory",   req.IsMandatory);
                cmd.Parameters.AddWithValue("@message",     req.UpdateMessage ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@storeUrl",    req.StoreUrl      ?? (object)DBNull.Value);

                await cmd.ExecuteNonQueryAsync();

                return Ok(new
                {
                    message     = "App version updated successfully.",
                    platform    = req.Platform,
                    version     = req.Version,
                    versionCode = req.VersionCode,
                    mandatory   = req.IsMandatory
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Version update failed.", error = ex.Message });
            }
        }

        public class UpdateVersionRequest
        {
            /// <summary>'android' or 'ios'</summary>
            public string Platform      { get; set; }
            /// <summary>Human-readable string e.g. "1.1.0"</summary>
            public string Version       { get; set; }
            /// <summary>Integer build number e.g. 11 — app compares this</summary>
            public int    VersionCode   { get; set; }
            /// <summary>true = no "Later" button, user must update to proceed</summary>
            public bool   IsMandatory   { get; set; }
            public string UpdateMessage { get; set; }
            public string StoreUrl      { get; set; }
        }
    }
}
