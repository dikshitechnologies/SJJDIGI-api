using JEWELLBISREACT.DBConnection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using static CHITSCHEME.Models.Rate_Fixing;

namespace CHITSCHEME.Controllers.Jewellery
{
    [Route("api/[controller]")]
    [ApiController]
    public class RatefixingController : ControllerBase
    {

        //--------------------------------------------------------------Get RateFixing  ------------------------------------------------------

        [HttpGet("getFullRateFixing")]
        public async Task<IActionResult> GetFullRateFixing()
        {
            var response = new RateFixingData();

            try
            {
                using var con = new SqlConnection(DBHelper.GetConnection());
                await con.OpenAsync();

                using (var cmd = new SqlCommand("SELECT FCODE, FNAME, FRATE FROM Division ORDER BY FNAME ASC", con))
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        response.DivisionData.Add(new RateFixing
                        {
                            FCODE = reader["FCODE"].ToString(),
                            FNAME = reader["FNAME"].ToString().ToUpper(),
                            FRATE = reader["FRATE"].ToString()
                        });
                    }
                }
                using (var cmd = new SqlCommand("SELECT FOLDGOLDVA, FOLDGOLDDUST, FOLDGOLDRATE, FOLDSILVERVA, FOLDSILVERDUST, FOLDSILVERRATE FROM RateFix WHERE 1=1", con))
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        response.RateFixData.Add(new OldRateFix
                        {
                            FOLDGOLDVA = reader["FOLDGOLDVA"].ToString(),
                            FOLDGOLDDUST = reader["FOLDGOLDDUST"].ToString(),
                            FOLDGOLDRATE = reader["FOLDGOLDRATE"].ToString(),
                            FOLDSILVERVA = reader["FOLDSILVERVA"].ToString(),
                            FOLDSILVERDUST = reader["FOLDSILVERDUST"].ToString(),
                            FOLDSILVERRATE = reader["FOLDSILVERRATE"].ToString()
                        });
                    }
                }

                return Ok(response);
            }
            catch (SqlException sqlEx)
            {
                return StatusCode(500, new { message = "SQL Error", sqlEx.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Internal Server Error", ex.Message });
            }
        }

        //--------------------------------------------------------------update  RateFixing  ------------------------------------------------------

        [HttpPut("updateFullRateFixing")]
        public async Task<IActionResult> UpdateFullRateFixing([FromBody] FullRateFixingRequest request)
        {
            try
            {
                using var con = new SqlConnection(DBHelper.GetConnection());
                await con.OpenAsync();
                using var transaction = con.BeginTransaction();

                try
                {
                    var updateDivisionQuery = @"
                UPDATE Division SET FRATE = @FRATE WHERE FCODE = @FCODE";

                    foreach (var division in request.Division)
                    {
                        using (var cmd = new SqlCommand(updateDivisionQuery, con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@FCODE", division.FCODE ?? "");
                            cmd.Parameters.AddWithValue("@FRATE", division.FRATE ?? "");
                            await cmd.ExecuteNonQueryAsync();
                        }
                    }

                    var checkQuery = "SELECT COUNT(*) FROM RateFix";
                    using var checkCmd = new SqlCommand(checkQuery, con, transaction);
                    int count = (int)await checkCmd.ExecuteScalarAsync();

                    string rateFixQuery;

                    if (count == 0)
                    {
                        rateFixQuery = @"
                    INSERT INTO RateFix 
                    (FOLDGOLDVA, FOLDGOLDDUST, FOLDGOLDRATE, FOLDSILVERVA, FOLDSILVERDUST, FOLDSILVERRATE)
                    VALUES 
                    (@FOLDGOLDVA, @FOLDGOLDDUST, @FOLDGOLDRATE, @FOLDSILVERVA, @FOLDSILVERDUST, @FOLDSILVERRATE)";
                    }
                    else
                    {
                        rateFixQuery = @"
                    UPDATE RateFix SET 
                        FOLDGOLDVA = @FOLDGOLDVA,
                        FOLDGOLDDUST = @FOLDGOLDDUST,
                        FOLDGOLDRATE = @FOLDGOLDRATE,
                        FOLDSILVERVA = @FOLDSILVERVA,
                        FOLDSILVERDUST = @FOLDSILVERDUST,
                        FOLDSILVERRATE = @FOLDSILVERRATE";
                    }

                    using (var cmd = new SqlCommand(rateFixQuery, con, transaction))
                    {
                        cmd.Parameters.AddWithValue("@FOLDGOLDVA", request.RateFix.FOLDGOLDVA ?? "");
                        cmd.Parameters.AddWithValue("@FOLDGOLDDUST", request.RateFix.FOLDGOLDDUST ?? "");
                        cmd.Parameters.AddWithValue("@FOLDGOLDRATE", request.RateFix.FOLDGOLDRATE ?? "");
                        cmd.Parameters.AddWithValue("@FOLDSILVERVA", request.RateFix.FOLDSILVERVA ?? "");
                        cmd.Parameters.AddWithValue("@FOLDSILVERDUST", request.RateFix.FOLDSILVERDUST ?? "");
                        cmd.Parameters.AddWithValue("@FOLDSILVERRATE", request.RateFix.FOLDSILVERRATE ?? "");
                        await cmd.ExecuteNonQueryAsync();
                    }

                    await transaction.CommitAsync();
                    return Ok(new { message = "Division and RateFix updated successfully" });
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    return StatusCode(500, new { message = "Transaction failed", error = ex.Message });
                }
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { message = "SQL Error", error = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Internal Server Error", error = ex.Message });
            }
        }



    }
}
