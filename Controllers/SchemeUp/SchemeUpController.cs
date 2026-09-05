using JEWELLBISREACT.DBConnection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;

namespace CHITSCHEME.Controllers.SchemeUp
{
    [Route("api/[controller]")]
    [ApiController]
    public class SchemeUpController : ControllerBase
    {
        // GET: api/SchemeUp/parties
        [HttpGet("parties")]
        public async Task<IActionResult> GetParties()
        {
            string query = @"
                SELECT 
                    fcode,
                    facName,
                    fShow
                FROM party
                WHERE fParent LIKE @Parent + '%'
                  AND faclevel > 2
                ORDER BY facName";

            var parties = new List<object>();

            using (SqlConnection connection =
                   new SqlConnection(DBHelper.GetConnection()))
            {
                await connection.OpenAsync();

                using (SqlCommand command =
                       new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@Parent", "0000100044");

                    using (SqlDataReader reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            parties.Add(new
                            {
                                fcode = reader["fcode"]?.ToString(),
                                facName = reader["facName"]?.ToString(),
                                fShow = reader["fShow"] == DBNull.Value
                                    ? 0
                                    : Convert.ToInt32(reader["fShow"])
                            });
                        }
                    }
                }
            }

            return Ok(parties);
        }


        // PUT: api/SchemeUp/party-show
        [HttpPut("party-show")]
        public async Task<IActionResult> UpdatePartyShow(
            [FromBody] UpdatePartyShowRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.fcode))
            {
                return BadRequest(new
                {
                    message = "fcode is required"
                });
            }

            if (request.fShow != 0 && request.fShow != 1)
            {
                return BadRequest(new
                {
                    message = "fShow must be either 0 or 1"
                });
            }

            string query = @"
                UPDATE party
                SET fShow = @fShow
                WHERE fcode = @fcode
                  AND fParent LIKE @Parent + '%'
                  AND faclevel > 2";

            using (SqlConnection connection =
                   new SqlConnection(DBHelper.GetConnection()))
            {
                await connection.OpenAsync();

                using (SqlCommand command =
                       new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@fcode", request.fcode);
                    command.Parameters.AddWithValue("@fShow", request.fShow);
                    command.Parameters.AddWithValue("@Parent", "0000100044");

                    int rowsAffected = await command.ExecuteNonQueryAsync();

                    if (rowsAffected == 0)
                    {
                        return NotFound(new
                        {
                            message = "Party not found"
                        });
                    }
                }
            }

            return Ok(new
            {
                message = "fShow updated successfully",
                fcode = request.fcode,
                fShow = request.fShow
            });
        }
    }

    public class UpdatePartyShowRequest
    {
        public string fcode { get; set; }
        public int fShow { get; set; }
    }
}