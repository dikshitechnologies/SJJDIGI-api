using JEWELLBISREACT.DBConnection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace CHITSCHEME.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SettingsController : ControllerBase
    {

        [HttpGet("getFeeSettings")]
        public async Task<IActionResult> GetFeeSettings()
        {
            FeeSettingsModel model = null;

            using (SqlConnection con = new SqlConnection(DBHelper.GetConnection()))
            {
                string query = @"SELECT TOP 1 FGstPercent, FPlatformFee FROM ILedger";

                using (SqlCommand cmd = new SqlCommand(query, con))
                {
                    await con.OpenAsync();

                    using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            model = new FeeSettingsModel
                            {
                                GstPercent = reader["FGstPercent"] == DBNull.Value
                                             ? 18
                                             : Convert.ToDecimal(reader["FGstPercent"]),

                                PlatformFee = reader["FPlatformFee"] == DBNull.Value
                                              ? 50
                                              : Convert.ToDecimal(reader["FPlatformFee"])
                            };
                        }
                        else
                        {
                            model = new FeeSettingsModel
                            {
                                GstPercent = 18,
                                PlatformFee = 50
                            };
                        }
                    }
                }
            }

            return Ok(model);
        }



        [HttpPut("updateFeeSettings")]
        public async Task<IActionResult> UpdateFeeSettings( string gst , string platformFee)
        {
            

            using (SqlConnection con = new SqlConnection(DBHelper.GetConnection()))
            {
                await con.OpenAsync();

                string query = @"UPDATE ILedger SET FGstPercent = @gst, FPlatformFee = @platformFee";

                using (SqlCommand cmd = new SqlCommand(query, con))
                {
                    cmd.Parameters.AddWithValue("@gst", gst);
                    cmd.Parameters.AddWithValue("@platformFee", platformFee);

                    await cmd.ExecuteNonQueryAsync();  // Correct for UPDATE
                }
            }

            return Ok(new
            {
                success = true,
                message = "Updated Successfully",
                gstPercent = gst,
                platformFee = platformFee
            });
        }


    }
}


public class FeeSettingsModel
{
    public decimal GstPercent { get; set; }
    public decimal PlatformFee { get; set; }
}
