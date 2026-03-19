using JEWELLBISREACT.DBConnection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace CHITSCHEME.Controllers.SHEME
{
    [Route("api/[controller]")]
    [Authorize]
    [ApiController]
    public class SchemeController : ControllerBase
    {
        private readonly string _connectionString;

       


        public class PartyDto
        {
            public string FCode { get; set; }
            public string FacName { get; set; }
            public string FParent { get; set; }
            [JsonPropertyName("fAmount")]
            public string FAMOUNT { get; set; }

            [JsonPropertyName("fDue")]
            public string fdue { get; set; }
            [JsonPropertyName("fschemetype")]

            public string schemeType { get; set; }

        }

        [HttpGet("SchemeList")]
        public async Task<IActionResult> GetPartyList()
        {
            var partyList = new List<PartyDto>();

            try
            {
                using (SqlConnection conn = new SqlConnection(DBHelper.GetConnection()))
                {
                    await conn.OpenAsync();

                    string query = @"
                        SELECT fcode, facName, fParent,fdue,FAMOUNT,FSCHEMETYPE
                        FROM party
                        WHERE fParent LIKE '0000100044' + '%' AND faclevel > 2 AND fShow ='1'
                        ORDER BY fParent, fcode";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                partyList.Add(new PartyDto
                                {
                                    FCode = reader["fcode"]?.ToString(),
                                    FacName = reader["facName"]?.ToString(),
                                    FParent = reader["fParent"]?.ToString(),
                                    fdue = reader["fdue"]?.ToString(),
                                    FAMOUNT = reader["FAMOUNT"]?.ToString(),
                                    schemeType = reader["FSCHEMETYPE"]?.ToString(),
                                });
                            }
                        }
                    }
                }

                return Ok(partyList);
            }
            catch (SqlException ex)
            {
                return StatusCode(500, new { error = "Database error occurred.", details = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Unexpected error occurred.", details = ex.Message });
            }
        }




        //---------------------------------------------------------------------------------------------------------------

        // -----------------------Fetch FPREFIX and FLEN-----------------------------
        private (string, int) GetPrefixAndLength(SqlConnection con, SqlTransaction transaction, string fCode)
        {
            string prefix = "";
            int flen = 0;

            using (SqlCommand cmd = new SqlCommand("SELECT FPREFIX, FLEN FROM party WHERE fCode = @fCode", con, transaction))
            {
                cmd.Parameters.AddWithValue("@fCode", fCode);
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        prefix = reader["FPREFIX"].ToString();
                        flen = Convert.ToInt32(reader["FLEN"]);
                    }
                }
            }
            return (prefix, flen);
        }

        //------------------------------- Get the next available fcode-------------------------------
        private string GetNextFcode(SqlConnection con, SqlTransaction transaction, string prefix, int flen)
        {
            string nextFcode = "";
            string queryMaxFcode = "SELECT MAX(fid) FROM party WHERE fid LIKE @PrefixPattern";

            using (SqlCommand cmd = new SqlCommand(queryMaxFcode, con, transaction))
            {
                cmd.Parameters.AddWithValue("@PrefixPattern", prefix + "%");
                var result = cmd.ExecuteScalar();

                if (result != null && result != DBNull.Value)
                {
                    string maxFcode = result.ToString();
                    string numberPart = maxFcode.Substring(prefix.Length);
                    int nextNumber = int.Parse(numberPart) + 1;
                    nextFcode = prefix + nextNumber.ToString().PadLeft(flen, '0');
                }
                else
                {
                    nextFcode = prefix + "1".PadLeft(flen, '0');
                }
            }

            // Check if max limit reached
            string maxPossibleCode = prefix + new string('9', flen);
            if (string.Compare(nextFcode, maxPossibleCode) > 0)
            {
                return "LIMIT_REACHED";
            }

            return nextFcode;
        }












        [HttpPost]
        [Route("JoinScheme")]
        public async Task<IActionResult> Register([FromBody] CustomerRegistrationPayload customer)
        {
            if (customer == null)
                return BadRequest(new { message = "Invalid data!" });

            SqlConnection con = null;
            SqlTransaction transaction = null;

            try
            {
                con = new SqlConnection(DBHelper.GetConnection());
                await con.OpenAsync();
                transaction = con.BeginTransaction();

                // ------------------ Fetch prefix and length ------------------
                var (prefix, flen) = GetPrefixAndLength(con, transaction, customer.schemeId);
                if (string.IsNullOrEmpty(prefix) || flen <= 0)
                    return BadRequest(new { message = "Invalid prefix or length." });

                // ------------------ Generate next customer code (fid) ------------------
                string nextCusCode = GetNextFcode(con, transaction, prefix, flen);
                if (nextCusCode == "LIMIT_REACHED")
                    return BadRequest(new { message = "Code limit reached!" });

                // ------------------ Generate next fCode ------------------
                string nextFcode = "";
                using (SqlCommand cmd = new SqlCommand("SELECT MAX(fcode) FROM party", con, transaction))
                {
                    var result = await cmd.ExecuteScalarAsync();
                    if (result != null)
                    {
                        string maxFcode = result.ToString();
                        int numericPart = int.Parse(maxFcode.TrimStart('0')) + 1;
                        nextFcode = numericPart.ToString().PadLeft(maxFcode.Length, '0');
                    }
                }

                //// ------------------ Fetch scheme details ------------------
                //string fType = "", fCategory = "", fFlex = "";
                //using (SqlCommand cmd = new SqlCommand("SELECT fType, fCategory, fFlex FROM party WHERE fcode = @schemeId", con, transaction))
                //{
                //    cmd.Parameters.AddWithValue("@schemeId", customer.schemeId);
                //    using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                //    {
                //        if (await reader.ReadAsync())
                //        {
                //            fType = reader["fType"]?.ToString();
                //            fCategory = reader["fCategory"]?.ToString();
                //            fFlex = reader["fFlex"]?.ToString();
                //        }
                //    }
                //}

                // ------------------ Fetch parent value for fparent ------------------
                string parentValue = "";
                using (SqlCommand cmd = new SqlCommand("SELECT fParent FROM party WHERE fcode = @fcode;", con, transaction))
                {
                    cmd.Parameters.AddWithValue("@fcode", customer.schemeId);
                    var result = await cmd.ExecuteScalarAsync();
                    parentValue = result?.ToString() ?? "";
                }

                // ------------------ Determine digiType and fschemetype ------------------
                string digiTypeValue = ""; // DG / DS / empty
                string fschemetypeValue = customer.fschemetype ?? "";

                if (!string.IsNullOrEmpty(customer.digiType))
                {
                    string digiUpper = customer.digiType.ToUpper();
                    if (digiUpper=="DIGI GOLD" || digiUpper == "DG")
                        digiTypeValue = "DG";
                    else if (digiUpper== "DIGI SILVER" || digiUpper == "DS")
                        digiTypeValue = "DS";
                    else if (digiUpper == "CH" || digiUpper == "ch")
                        digiTypeValue = "CH";
                    else
                        return BadRequest(new { message = "Invalid digiType. Use 'DG', 'DS', 'Digi Gold', 'Digi Silver', 'None' or 'NA'." });

                    if (digiTypeValue != "") // If digital scheme
                        fschemetypeValue = "W"; // Override fschemetype for digital schemes
                }

                decimal finalDue = customer.due ?? 0;

                // If Digi Gold or Digi Silver → set due = 100
                if (digiTypeValue == "DG" || digiTypeValue == "DS")
                {
                    finalDue = 100;
                }

                // ------------------ Insert customer into party ------------------
                string insertQuery = @"
            INSERT INTO party 
            (fcode, facname, fparent, faclevel, fstreet, farea, fcity, fpincode, fphone, fmail, fdate, famount, fdue, fid,   fschemetype, FdigiType, Fdigicr)
            VALUES 
            (@fcode, @facname, @fparent, @faclevel, @fstreet, @farea, @fcity, @fpincode, @fphone, @fmail, @fdate, @famount, @fdue,  @fid,   @fschemetype, @digiType, @Fdigicr);";

                using (SqlCommand cmd = new SqlCommand(insertQuery, con, transaction))
                {
                    cmd.Parameters.AddWithValue("@fcode", nextFcode);
                    cmd.Parameters.AddWithValue("@fid", nextCusCode);
                    cmd.Parameters.AddWithValue("@facname", customer.fullName);
                    cmd.Parameters.AddWithValue("@fparent", parentValue + nextFcode);
                    cmd.Parameters.AddWithValue("@faclevel", "-4");
                    cmd.Parameters.AddWithValue("@fstreet", customer.street ?? "");
                    cmd.Parameters.AddWithValue("@farea", customer.area ?? "");
                    cmd.Parameters.AddWithValue("@fcity", customer.city ?? "");
                    cmd.Parameters.AddWithValue("@fpincode", customer.pincode ?? "");
                    cmd.Parameters.AddWithValue("@fphone", customer.phone ?? "");
                    cmd.Parameters.AddWithValue("@fmail", customer.email ?? "");
                    cmd.Parameters.AddWithValue("@fdate", DateTime.Now);
                    cmd.Parameters.AddWithValue("@famount", customer.amount);
                    cmd.Parameters.AddWithValue("@fdue", finalDue);
                    cmd.Parameters.AddWithValue("@fschemetype", fschemetypeValue.ToUpper());
                    cmd.Parameters.AddWithValue("@digiType", digiTypeValue);
                    cmd.Parameters.AddWithValue("@Fdigicr", customer.digiCr.ToUpper() ?? "");

                    await cmd.ExecuteNonQueryAsync();
                }

                transaction.Commit();

                return Ok(new
                {
                    message = "Customer registered successfully!",
                    customerCode = nextCusCode,
                    schemeCode = customer.schemeId,
                    name = customer.fullName,
                    phone = customer.phone,
                    fschemetype = fschemetypeValue,
                    digiType = digiTypeValue
                });
            }
            catch (Exception ex)
            {
                transaction?.Rollback();
                return StatusCode(500, new { message = "Error registering customer", error = ex.Message });
            }
            finally
            {
                con?.Close();
            }
        }

        // ------------------ Updated DTO ------------------
        public class CustomerRegistrationPayload
        {
            public string fullName { get; set; }
            public string schemeId { get; set; }
            public string? street { get; set; }
            public string? area { get; set; }
            public string? city { get; set; }
            public string? pincode { get; set; }
            public string phone { get; set; }
            public string? email { get; set; }
            public string fschemetype { get; set; } // Will be overridden for digital schemes
            public decimal? amount { get; set; }
            public decimal? due { get; set; }
            public string digiType { get; set; }   // Gold/Silver
            public string? digiCr { get; set; }     // optional
        }



    }
}

