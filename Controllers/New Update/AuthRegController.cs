using CHITSCHEME.Helpers;
using JEWELLBISREACT.DBConnection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Authorization;
using static QRCoder.PayloadGenerator;

namespace CHITSCHEME.Controllers.Jewellery
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthRegController : ControllerBase
    {
        private readonly IConfiguration _config;

        public AuthRegController(IConfiguration config)
        {
            _config = config;
        }
        [AllowAnonymous]
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Phone))
                return BadRequest(new { message = "Phone number is required." });

            if (request.Phone.Length != 10)
                return BadRequest(new { message = "Phone number must be 10 digits." });

            if (!IsPhoneNumberValid(request.Phone))
                return BadRequest(new { message = "Invalid phone number format." });

            var responseDivisions = new
            {
                divisions = new
                {
                    gold = new List<object>(),
                    silver = new List<object>()
                }
            };

            try
            {
                var connectionString = DBHelper.GetConnection();
                using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();

                // -------- Check Party --------
                string partyName = null;
                string partyPhone = null;

                var partyCmd = new SqlCommand(@"
            SELECT TOP 1 FACNAME, FPHONE 
            FROM Party 
            WHERE faclevel < 0 
              AND fparent LIKE '0000100044%' 
              AND fphone = @phone
            ORDER BY FCODE", connection);
                partyCmd.Parameters.AddWithValue("@phone", request.Phone);

                using (var reader = await partyCmd.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        partyName = reader["FACNAME"].ToString();
                        partyPhone = reader["FPHONE"].ToString();
                    }
                }

                // -------- Check RegisterUsers --------
                string username = string.Empty;
                string email = string.Empty;
                int userId = 0;

                var regDetailsCmd = new SqlCommand(
                    "SELECT UserId, UserName, Email,PhoneNumber FROM RegisterUsers WHERE PhoneNumber = @phone",
                    connection);
                regDetailsCmd.Parameters.AddWithValue("@phone", request.Phone);

                using (var reader = await regDetailsCmd.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        userId = Convert.ToInt32(reader["UserId"]);
                        username = reader["UserName"].ToString();
                        email = reader["Email"].ToString();
                        partyPhone = reader["PhoneNumber"].ToString();
                    }
                }

                // -------- If party exists but not registered, insert new user --------
                if (partyName != null && partyPhone != null && userId == 0)
                {
                    using var transaction = connection.BeginTransaction();

                    try
                    {
                        var getMaxIdCmd = new SqlCommand(
                            "SELECT ISNULL(MAX(UserId), 1000) + 1 FROM RegisterUsers WITH (TABLOCKX)",
                            connection, transaction);
                        userId = (int)await getMaxIdCmd.ExecuteScalarAsync();

                        var insertCmd = new SqlCommand(@"
                   INSERT INTO RegisterUsers (UserId, UserName, PhoneNumber, Email, PasswordHash, CreatedAt, FcmToken, DeviceType, LastLogin)
                    VALUES (@UserId, @UserName, @PhoneNumber, @Email, @PasswordHash, @CreatedAt, @FcmToken, @DeviceType, @LastLogin)",
                            connection, transaction);

                        insertCmd.Parameters.AddWithValue("@UserId", userId);
                        insertCmd.Parameters.AddWithValue("@UserName", partyName);
                        insertCmd.Parameters.AddWithValue("@PhoneNumber", partyPhone);
                        insertCmd.Parameters.AddWithValue("@Email", "");
                        insertCmd.Parameters.AddWithValue("@PasswordHash", "");
                        insertCmd.Parameters.AddWithValue("@CreatedAt", DateTime.Now);
                        insertCmd.Parameters.AddWithValue("@FcmToken", (object)request.FcmToken ?? DBNull.Value);
                        insertCmd.Parameters.AddWithValue("@DeviceType", (object)request.DeviceType ?? DBNull.Value);
                        insertCmd.Parameters.AddWithValue("@LastLogin", DateTime.Now);
                        await insertCmd.ExecuteNonQueryAsync();
                        await transaction.CommitAsync();
                        if (userId !=0 && !string.IsNullOrEmpty(partyPhone))
                        {


                            // Fetch Division Data
                            var divisionCmd = new SqlCommand(
                                @"SELECT fCode, fName, fRate 
                                FROM Division 
                               WHERE fCode IN ('0003','0002','0014','0004','0005')",
                                connection);

                            using (var reader = await divisionCmd.ExecuteReaderAsync())
                            {
                                while (await reader.ReadAsync())
                                {
                                    string code = reader["fCode"].ToString();
                                    string name = reader["fName"].ToString();
                                    decimal rate = Convert.ToDecimal(reader["fRate"]);
                                    if (code == "0002")
                                    {
                                        responseDivisions.divisions.gold.Add(new
                                        {
                                            name = "22K",
                                            rate
                                        });
                                    }

                                    // SILVER
                                    if (code == "0005")
                                    {
                                        responseDivisions.divisions.silver.Add(new
                                        {
                                            name = "SILVER",
                                            rate
                                        });
                                    }
                                }
                            }


                        }


                    }
                    catch
                    {
                        await transaction.RollbackAsync();
                        throw;
                    }



                    var tokenNew = JwtHelper.GenerateJwtToken(request.Phone, "User", _config);
                    return Ok(new { token = tokenNew, UserPermission = "U", UserId = userId, username = partyName, email = "" , phone = partyPhone, responseDivisions });
                }

                // -------- If party exists and already registered --------
                if (partyName != null && partyPhone != null && userId > 0)
                {
                    var updateCmd = new SqlCommand(@"
                        UPDATE RegisterUsers 
                        SET FcmToken = @FcmToken, DeviceType = @DeviceType, LastLogin = @LastLogin
                        WHERE UserId = @UserId", connection);
                    updateCmd.Parameters.AddWithValue("@FcmToken", (object)request.FcmToken ?? DBNull.Value);
                    updateCmd.Parameters.AddWithValue("@DeviceType", (object)request.DeviceType ?? DBNull.Value);
                    updateCmd.Parameters.AddWithValue("@LastLogin", DateTime.Now);
                    updateCmd.Parameters.AddWithValue("@UserId", userId);
                    await updateCmd.ExecuteNonQueryAsync();

                    var token = JwtHelper.GenerateJwtToken(request.Phone, "User", _config);
                    return Ok(new { token, UserPermission = "U", UserId = userId, username, email, phone = partyPhone, responseDivisions });
                }

                // -------- If party does not exist but user is registered --------
                if (partyName == null && userId > 0)
                {
                    var updateCmd = new SqlCommand(@"
                        UPDATE RegisterUsers 
                        SET FcmToken = @FcmToken, DeviceType = @DeviceType, LastLogin = @LastLogin
                        WHERE UserId = @UserId", connection);
                    updateCmd.Parameters.AddWithValue("@FcmToken", (object)request.FcmToken ?? DBNull.Value);
                    updateCmd.Parameters.AddWithValue("@DeviceType", (object)request.DeviceType ?? DBNull.Value);
                    updateCmd.Parameters.AddWithValue("@LastLogin", DateTime.Now);
                    updateCmd.Parameters.AddWithValue("@UserId", userId);
                    await updateCmd.ExecuteNonQueryAsync();

                    var token = JwtHelper.GenerateJwtToken(request.Phone, "User", _config);
                    return Ok(new { token, UserPermission = "U", UserId = userId, username, email , phone = partyPhone, responseDivisions });
                }

                // -------- Otherwise --------
                return Unauthorized(new { message = "Please check the phone number." });
            }
            catch (SqlException)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Database error. Please try again later." });
            }
            catch (Exception)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An unexpected error occurred. Please try again later." });
            }
        }



        private bool IsPhoneNumberValid(string phone)
        {

            var regex = new Regex(@"^\d{10}$");
            return regex.IsMatch(phone);
        }



        [HttpGet("AdminValidate/{username}/{password}")]
        public async Task<IActionResult> AdminValidate([FromRoute] string username, [FromRoute] string password)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                return BadRequest(new { message = "Username and password are required." });

            try
            {

                var responseDivisions = new
                {
                    divisions = new
                    {
                        gold = new List<object>(),
                        silver = new List<object>()
                    }
                };





                var connectionString = DBHelper.GetConnection();
                using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();


                // Fetch Division Data
                var divisionCmd = new SqlCommand(
                    @"SELECT fCode, fName, fRate 
            FROM Division 
           WHERE fCode IN ('0003','0002','0014','0004','0005')",
                    connection);

                using (var reader1 = await divisionCmd.ExecuteReaderAsync())
                {
                    while (await reader1.ReadAsync())
                    {
                        string code = reader1["fCode"].ToString();
                        string name = reader1["fName"].ToString();
                        decimal rate = Convert.ToDecimal(reader1["fRate"]);

                        // GOLD: 14K, 18K, 22K, 24K
                        if (code == "0003" || code == "0002" || code == "0014" || code == "0004")
                        {
                            responseDivisions.divisions.gold.Add(new
                            {
                                name,
                                rate
                            });
                        }

                        // SILVER
                        if (code == "0005")
                        {
                            responseDivisions.divisions.silver.Add(new
                            {
                                name,
                                rate
                            });
                        }
                    }
                }
                // ✅ Updated: Select more columns
                var cmd = new SqlCommand(@"
            SELECT TOP 1 FCOMPCODE, FADMIN, PHONE1
            FROM COMPANY 
            WHERE  FSUP = @username AND FADMIN = @password", connection);

                cmd.Parameters.AddWithValue("@username", username);
                cmd.Parameters.AddWithValue("@password", password);

                using var reader = await cmd.ExecuteReaderAsync();


                if (await reader.ReadAsync())
                {
                    string fcompcode = reader["FCOMPCODE"].ToString();
                    string adminName = reader["FADMIN"].ToString();
                    string phone = reader["PHONE1"].ToString();

                    // ✅ Optionally use phone in token
                    var token = JwtHelper.GenerateJwtToken(phone, "Admin", _config);

                    return Ok(new
                    {
                        role = "Admin",
                        token,
                        UserPermission = "A",
                        UserId = fcompcode,
                        AdminName = adminName,
                        Phone = phone,
                        responseDivisions
                    });
                }
                else
                {
                    return Unauthorized(new { message = "Invalid admin credentials." });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Error validating admin.", error = ex.Message });
            }
        }

        [AllowAnonymous]
        [HttpPost("guest-login")]
        public async Task<IActionResult> GuestLogin()
        {
            try
            {


                var responseDivisions = new
                {
                    divisions = new
                    {
                        gold = new List<object>(),
                        silver = new List<object>()
                    }
                };

                var connectionString = DBHelper.GetConnection();
                using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();

                // Fetch Division Data
                var divisionCmd = new SqlCommand(
                    @"SELECT fCode, fName, fRate 
            FROM Division 
           WHERE fCode IN ('0003','0002','0014','0004','0005')",
                    connection);

                using (var reader = await divisionCmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        string code = reader["fCode"].ToString();
                        string name = reader["fName"].ToString();
                        decimal rate = Convert.ToDecimal(reader["fRate"]);

                        // GOLD: 14K, 18K, 22K, 24K
                        if (code == "0003" || code == "0002" || code == "0014" || code == "0004")
                        {
                            responseDivisions.divisions.gold.Add(new
                            {
                                name,
                                rate
                            });
                        }

                        // SILVER
                        if (code == "0005")
                        {
                            responseDivisions.divisions.silver.Add(new
                            {
                                name,
                                rate
                            });
                        }
                    }
                }


                // Generate a random GuestId
                string guestId = Guid.NewGuid().ToString("N").Substring(0, 10);

                // Generate JWT token with Guest role
                var token = JwtHelper.GenerateJwtToken(guestId, "Guest", _config);

                return Ok(new
                {
                    role = "Guest",
                    token,
                    UserPermission = "G",
                    GuestId = guestId,
                    username = "Guest User",
                    email = "",
                    responseDivisions
                });
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Error creating guest login.", error = ex.Message });
            }
        }







    }
}

public class AuthLoginRequest
{
    public string Phone { get; set; }
}