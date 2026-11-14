
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using JEWELLBISREACT.DBConnection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace CHITSCHEME.Controllers.FireBaseMsg
{
    [Route("api/[controller]")]
    [ApiController]
    public class FBNotificationController : ControllerBase
    {
        private const string FirebaseJsonFile =
            "sri-jain-walajapet-firebase-adminsdk-fbsvc-656f56a0e7.json";

        private static bool _firebaseInitialized = false;
        private static readonly object _lock = new object();

        private readonly ILogger<FBNotificationController> _logger;

        public FBNotificationController(
            ILogger<FBNotificationController> logger)
        {
            _logger = logger;
        }

        // ============================================================
        // FIREBASE INITIALIZATION WITH DETAILED LOGGING
        // ============================================================

        private void EnsureFirebaseInitialized()
        {
            if (_firebaseInitialized)
            {
                _logger.LogInformation(
                    "[FIREBASE_INIT] Firebase already initialized.");
                return;
            }

            lock (_lock)
            {
                if (_firebaseInitialized)
                    return;

                string baseDirectory = AppContext.BaseDirectory;

                string jsonPath = Path.Combine(
                    baseDirectory,
                    FirebaseJsonFile
                );

                _logger.LogInformation(
                    "[FIREBASE_INIT] BaseDirectory: {BaseDirectory}",
                    baseDirectory
                );

                _logger.LogInformation(
                    "[FIREBASE_INIT] JSON Path: {JsonPath}",
                    jsonPath
                );

                _logger.LogInformation(
                    "[FIREBASE_INIT] JSON Exists: {Exists}",
                    System.IO.File.Exists(jsonPath)
                );

                if (!System.IO.File.Exists(jsonPath))
                {
                    throw new FileNotFoundException(
                        "Firebase service account JSON file not found.",
                        jsonPath
                    );
                }

                var fileInfo = new FileInfo(jsonPath);

                _logger.LogInformation(
                    "[FIREBASE_INIT] JSON File Size: {Length} bytes",
                    fileInfo.Length
                );

                // Log safe metadata only. Never log private_key.
                try
                {
                    using var document = JsonDocument.Parse(
                        System.IO.File.ReadAllText(jsonPath)
                    );

                    var root = document.RootElement;

                    string projectId =
                        root.TryGetProperty("project_id", out var project)
                            ? project.GetString() ?? "Not found"
                            : "Not found";

                    string privateKeyId =
                        root.TryGetProperty("private_key_id", out var keyId)
                            ? keyId.GetString() ?? "Not found"
                            : "Not found";

                    _logger.LogInformation(
                        "[FIREBASE_INIT] Project ID: {ProjectId}",
                        projectId
                    );

                    _logger.LogInformation(
                        "[FIREBASE_INIT] Private Key ID: {PrivateKeyId}",
                        privateKeyId
                    );
                }
                catch (Exception metadataEx)
                {
                    _logger.LogError(
                        metadataEx,
                        "[FIREBASE_INIT] Failed to read JSON metadata."
                    );

                    throw;
                }

                try
                {
                    if (FirebaseApp.DefaultInstance == null)
                    {
                        var credential =
                            GoogleCredential.FromFile(jsonPath);

                        FirebaseApp.Create(new AppOptions
                        {
                            Credential = credential
                        });

                        _logger.LogInformation(
                            "[FIREBASE_INIT] Firebase app created successfully."
                        );
                    }
                    else
                    {
                        _logger.LogInformation(
                            "[FIREBASE_INIT] Default Firebase app already exists. " +
                            "Existing app credentials will be reused."
                        );
                    }

                    _firebaseInitialized = true;

                    _logger.LogInformation(
                        "[FIREBASE_INIT] Firebase initialization completed."
                    );
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "[FIREBASE_INIT_ERROR] Firebase initialization failed. " +
                        "ExceptionType={ExceptionType}, Message={Message}, " +
                        "InnerException={InnerException}",
                        ex.GetType().FullName,
                        ex.Message,
                        ex.InnerException?.ToString()
                    );

                    throw;
                }
            }
        }

        // ============================================================
        // POST api/FBNotification/send
        // ============================================================

        [AllowAnonymous]
        [HttpPost("send")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> Send(
            [FromForm] string title,
            [FromForm] string body,
            IFormFile? image)
        {
            string requestId = HttpContext.TraceIdentifier;

            _logger.LogInformation(
                "[FCM_REQUEST] Notification request started. RequestId={RequestId}",
                requestId
            );

            if (string.IsNullOrWhiteSpace(title))
            {
                return BadRequest(new
                {
                    message = "Title is required.",
                    requestId
                });
            }

            if (string.IsNullOrWhiteSpace(body))
            {
                return BadRequest(new
                {
                    message = "Body is required.",
                    requestId
                });
            }

            try
            {
                // ----------------------------------------------------
                // 1. INITIALIZE FIREBASE
                // ----------------------------------------------------

                EnsureFirebaseInitialized();

                var messaging = FirebaseMessaging.DefaultInstance;

                _logger.LogInformation(
                    "[FCM_REQUEST] Firebase messaging instance obtained."
                );

                // ----------------------------------------------------
                // 2. HANDLE IMAGE UPLOAD
                // ----------------------------------------------------

                string? imageUrl = null;

                if (image != null && image.Length > 0)
                {
                    var allowedExtensions = new[]
                    {
                        ".jpg", ".jpeg", ".png", ".webp"
                    };

                    string extension =
                        Path.GetExtension(image.FileName).ToLowerInvariant();

                    if (!allowedExtensions.Contains(extension))
                    {
                        return BadRequest(new
                        {
                            message = "Unsupported image type. Use jpg, jpeg, png or webp.",
                            requestId
                        });
                    }

                    if (image.Length > 20 * 1024 * 1024)
                    {
                        return BadRequest(new
                        {
                            message = "Image too large. Maximum size is 20 MB.",
                            requestId
                        });
                    }

                    string notificationFolder = Path.Combine(
                        Directory.GetCurrentDirectory(),
                        "wwwroot",
                        "notification"
                    );

                    if (!Directory.Exists(notificationFolder))
                    {
                        Directory.CreateDirectory(notificationFolder);
                    }

                    string fileName =
                        $"{Guid.NewGuid()}{extension}";

                    string fullPath = Path.Combine(
                        notificationFolder,
                        fileName
                    );

                    using (var stream = new FileStream(
                        fullPath,
                        FileMode.Create))
                    {
                        await image.CopyToAsync(stream);
                    }

                    imageUrl =
                        $"https://app.dikshitech.com/SJJDIGI/notification/{fileName}";

                    _logger.LogInformation(
                        "[FCM_IMAGE] Image uploaded successfully. URL={ImageUrl}",
                        imageUrl
                    );
                }

                // ----------------------------------------------------
                // 3. GET REGISTERED FCM TOKENS
                // ----------------------------------------------------

                var tokens = new List<string>();

                string connStr = DBHelper.GetConnection();

                using var conn = new SqlConnection(connStr);

                await conn.OpenAsync();

                using (var cmd = new SqlCommand(@"
                    SELECT DISTINCT FcmToken
                    FROM RegisterUsers
                    WHERE FcmToken IS NOT NULL
                      AND LTRIM(RTRIM(FcmToken)) <> ''
                ", conn))
                {
                    using var reader = await cmd.ExecuteReaderAsync();

                    while (await reader.ReadAsync())
                    {
                        string token =
                            reader["FcmToken"]?.ToString() ?? "";

                        if (!string.IsNullOrWhiteSpace(token))
                        {
                            tokens.Add(token);
                        }
                    }
                }

                _logger.LogInformation(
                    "[FCM_TOKENS] Total registered tokens: {Count}",
                    tokens.Count
                );

                if (tokens.Count == 0)
                {
                    return Ok(new
                    {
                        message = "No registered devices found.",
                        sent = 0,
                        failed = 0,
                        requestId
                    });
                }

                // ----------------------------------------------------
                // 4. BUILD FCM PAYLOAD
                // ----------------------------------------------------

                var dataPayload = new Dictionary<string, string>
                {
                    { "click_action", "FLUTTER_NOTIFICATION_CLICK" }
                };

                if (!string.IsNullOrWhiteSpace(imageUrl))
                {
                    dataPayload["image_url"] = imageUrl;
                }

                // ----------------------------------------------------
                // 5. SEND NOTIFICATIONS IN BATCHES
                // ----------------------------------------------------

                const int batchSize = 500;

                int totalSent = 0;
                int totalFailed = 0;

                var invalidTokens = new List<string>();

                var failureDetails = new List<object>();

                for (int i = 0; i < tokens.Count; i += batchSize)
                {
                    var batch = tokens
                        .Skip(i)
                        .Take(batchSize)
                        .ToList();

                    _logger.LogInformation(
                        "[FCM_BATCH] Starting batch. BatchNumber={BatchNumber}, " +
                        "BatchSize={BatchSize}",
                        (i / batchSize) + 1,
                        batch.Count
                    );

                    var multicast = new MulticastMessage
                    {
                        Tokens = batch,

                        Notification = new Notification
                        {
                            Title = title,
                            Body = body,
                            ImageUrl = imageUrl
                        },

                        Data = dataPayload,

                        Android = new AndroidConfig
                        {
                            Priority = Priority.High,

                            Notification = new AndroidNotification
                            {
                                Title = title,
                                Body = body,
                                ImageUrl = imageUrl,
                                Sound = "default",
                                ChannelId = "general"
                            }
                        },

                        Apns = new ApnsConfig
                        {
                            Aps = new Aps
                            {
                                Sound = "default",
                                Badge = 1,
                                ContentAvailable = true
                            }
                        }
                    };

                    BatchResponse response;

                    try
                    {
                        response =
                            await messaging.SendEachForMulticastAsync(multicast);
                    }
                    catch (Exception batchEx)
                    {
                        // Authentication, JWT, project or network errors
                        // can fail the entire batch before individual responses.

                        totalFailed += batch.Count;

                        _logger.LogError(
                            batchEx,
                            "[FCM_BATCH_ERROR] Entire batch failed. " +
                            "BatchNumber={BatchNumber}, BatchSize={BatchSize}, " +
                            "ExceptionType={ExceptionType}, Message={Message}, " +
                            "InnerException={InnerException}",
                            (i / batchSize) + 1,
                            batch.Count,
                            batchEx.GetType().FullName,
                            batchEx.Message,
                            batchEx.InnerException?.ToString()
                        );

                        failureDetails.Add(new
                        {
                            batchNumber = (i / batchSize) + 1,
                            batchSize = batch.Count,
                            exceptionType = batchEx.GetType().FullName,
                            error = batchEx.Message,
                            innerError = batchEx.InnerException?.Message
                        });

                        continue;
                    }

                    totalSent += response.SuccessCount;
                    totalFailed += response.FailureCount;

                    _logger.LogInformation(
                        "[FCM_BATCH_RESULT] Batch={BatchNumber}, " +
                        "Success={Success}, Failed={Failed}",
                        (i / batchSize) + 1,
                        response.SuccessCount,
                        response.FailureCount
                    );

                    // ------------------------------------------------
                    // 6. LOG EACH FAILED TOKEN WITH ITS EXACT ERROR
                    // ------------------------------------------------

                    for (int j = 0; j < response.Responses.Count; j++)
                    {
                        var item = response.Responses[j];

                        if (item.IsSuccess)
                        {
                            _logger.LogInformation(
                                "[FCM_SUCCESS] Batch={BatchNumber}, " +
                                "TokenIndex={TokenIndex}",
                                (i / batchSize) + 1,
                                j
                            );

                            continue;
                        }

                        Exception? tokenException = item.Exception;

                        string errorMessage =
                            tokenException?.Message ?? "Unknown FCM error";

                        string exceptionType =
                            tokenException?.GetType().FullName ?? "Unknown";

                        string token = batch[j];

                        string maskedToken =
                            token.Length > 10
                                ? token.Substring(0, 6) + "..." +
                                  token.Substring(token.Length - 4)
                                : "***";

                        string? firebaseErrorCode = null;

                        bool isUnregistered = false;

                        if (tokenException is FirebaseMessagingException fcmEx)
                        {
                            firebaseErrorCode =
                                fcmEx.MessagingErrorCode.ToString();

                            isUnregistered =
                                fcmEx.MessagingErrorCode ==
                                MessagingErrorCode.Unregistered;
                        }

                        _logger.LogError(
                            tokenException,
                            "[FCM_TOKEN_FAILURE] Batch={BatchNumber}, " +
                            "TokenIndex={TokenIndex}, Token={Token}, " +
                            "ExceptionType={ExceptionType}, " +
                            "FirebaseErrorCode={FirebaseErrorCode}, " +
                            "Error={Error}, InnerException={InnerException}",
                            (i / batchSize) + 1,
                            j,
                            maskedToken,
                            exceptionType,
                            firebaseErrorCode,
                            errorMessage,
                            tokenException?.InnerException?.ToString()
                        );

                        failureDetails.Add(new
                        {
                            batchNumber = (i / batchSize) + 1,
                            tokenIndex = j,
                            maskedToken,
                            exceptionType,
                            firebaseErrorCode,
                            error = errorMessage
                        });

                        // Only remove tokens Firebase confirms are unregistered.
                        // Do not remove tokens for invalid JWT or auth failures.

                        if (isUnregistered)
                        {
                            invalidTokens.Add(token);
                        }
                    }
                }

                // ----------------------------------------------------
                // 7. CLEAR ONLY UNREGISTERED TOKENS
                // ----------------------------------------------------

                int removedTokens = 0;

                foreach (string staleToken in invalidTokens.Distinct())
                {
                    using var cleanCmd = new SqlCommand(@"
                        UPDATE RegisterUsers
                        SET FcmToken = NULL
                        WHERE FcmToken = @token
                    ", conn);

                    cleanCmd.Parameters.AddWithValue(
                        "@token",
                        staleToken
                    );

                    removedTokens +=
                        await cleanCmd.ExecuteNonQueryAsync();
                }

                _logger.LogInformation(
                    "[FCM_TOKEN_CLEANUP] Unregistered tokens cleared: {Count}",
                    removedTokens
                );

                // ----------------------------------------------------
                // 8. SAVE NOTIFICATION LOG
                // ----------------------------------------------------

                string notificationId = Guid.NewGuid().ToString();

                using (var insertLog = new SqlCommand(@"
                    INSERT INTO Notifications
                    (
                        NotificationId,
                        Title,
                        Body,
                        ImageUrl,
                        SentAt,
                        TotalDevices,
                        SuccessCount,
                        FailureCount
                    )
                    VALUES
                    (
                        @NotificationId,
                        @Title,
                        @Body,
                        @ImageUrl,
                        @SentAt,
                        @TotalDevices,
                        @SuccessCount,
                        @FailureCount
                    )
                ", conn))
                {
                    insertLog.Parameters.AddWithValue(
                        "@NotificationId",
                        notificationId
                    );

                    insertLog.Parameters.AddWithValue("@Title", title);
                    insertLog.Parameters.AddWithValue("@Body", body);

                    insertLog.Parameters.AddWithValue(
                        "@ImageUrl",
                        (object?)imageUrl ?? DBNull.Value
                    );

                    insertLog.Parameters.AddWithValue(
                        "@SentAt",
                        DateTime.Now
                    );

                    insertLog.Parameters.AddWithValue(
                        "@TotalDevices",
                        tokens.Count.ToString()
                    );

                    insertLog.Parameters.AddWithValue(
                        "@SuccessCount",
                        totalSent.ToString()
                    );

                    insertLog.Parameters.AddWithValue(
                        "@FailureCount",
                        totalFailed.ToString()
                    );

                    await insertLog.ExecuteNonQueryAsync();
                }

                // ----------------------------------------------------
                // 9. FINAL RESULT
                // ----------------------------------------------------

                _logger.LogInformation(
                    "[FCM_FINAL] RequestId={RequestId}, " +
                    "Total={Total}, Sent={Sent}, Failed={Failed}",
                    requestId,
                    tokens.Count,
                    totalSent,
                    totalFailed
                );

                return Ok(new
                {
                    message = totalFailed == 0
                        ? "Notification sent successfully."
                        : "Notification processing completed with failures.",

                    requestId,
                    notificationId,
                    imageUrl,

                    totalDevices = tokens.Count,
                    sent = totalSent,
                    failed = totalFailed,

                    removedUnregisteredTokens = removedTokens,

                    errors = failureDetails
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "[FCM_FATAL_ERROR] Notification request failed. " +
                    "RequestId={RequestId}, ExceptionType={ExceptionType}, " +
                    "Message={Message}, InnerException={InnerException}, " +
                    "StackTrace={StackTrace}",
                    requestId,
                    ex.GetType().FullName,
                    ex.Message,
                    ex.InnerException?.ToString(),
                    ex.StackTrace
                );

                return StatusCode(500, new
                {
                    message = "Failed to send Firebase notification.",

                    requestId,

                    exceptionType = ex.GetType().FullName,

                    error = ex.Message,

                    innerError = ex.InnerException?.Message
                });
            }
        }
    }
}
