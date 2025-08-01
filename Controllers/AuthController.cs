using CHITSCHEME.Helpers;
using JEWELLBISREACT.DBConnection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Authorization;

namespace CHITSCHEME.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IConfiguration _config;

        public AuthController(IConfiguration config)
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

            try
            {
                var connectionString = DBHelper.GetConnection();
                using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();

                var partyCmd = new SqlCommand(@"
            SELECT TOP 1 FACNAME, FPHONE 
            FROM Party 
            WHERE faclevel < 0 
              AND fparent LIKE '0000100044%' 
              AND fphone = @phone
            ORDER BY FCODE", connection);
                partyCmd.Parameters.AddWithValue("@phone", request.Phone);

                string partyName = null;
                string partyPhone = null;

                using (var reader = await partyCmd.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        partyName = reader["FACNAME"].ToString();
                        partyPhone = reader["FPHONE"].ToString();
                    }
                }

                int userId = 0;
                var userCheckCmd = new SqlCommand("SELECT UserId FROM RegisterUsers WHERE PhoneNumber = @phone", connection);
                userCheckCmd.Parameters.AddWithValue("@phone", request.Phone);
                var result = await userCheckCmd.ExecuteScalarAsync();
                if (result != null)
                    userId = Convert.ToInt32(result);

                if ((partyName != null && partyPhone != null) && userId == 0)
                {
                    using (var transaction = connection.BeginTransaction())
                    {
                        var getMaxIdCmd = new SqlCommand(
                            "SELECT ISNULL(MAX(UserId), 1000) + 1 FROM RegisterUsers WITH (TABLOCKX)",
                            connection, transaction);
                        userId = (int)await getMaxIdCmd.ExecuteScalarAsync();

                        var insertCmd = new SqlCommand(@"
                    INSERT INTO RegisterUsers (UserId, UserName, PhoneNumber, Email, PasswordHash, CreatedAt)
                    VALUES (@UserId, @UserName, @PhoneNumber, @Email, @PasswordHash, @CreatedAt)", connection, transaction);
                        insertCmd.Parameters.AddWithValue("@UserId", userId);
                        insertCmd.Parameters.AddWithValue("@UserName", partyName);
                        insertCmd.Parameters.AddWithValue("@PhoneNumber", partyPhone);
                        insertCmd.Parameters.AddWithValue("@Email", "");
                        insertCmd.Parameters.AddWithValue("@PasswordHash", "");
                        insertCmd.Parameters.AddWithValue("@CreatedAt", DateTime.Now);

                        await insertCmd.ExecuteNonQueryAsync();
                        transaction.Commit();
                    }

                    var token = JwtHelper.GenerateJwtToken(request.Phone,"User", _config);
                    return Ok(new { token, UserPermission = "Y", UserId = userId });
                }
                else if ((partyName != null && partyPhone != null) && userId > 0)
                {
                    var token = JwtHelper.GenerateJwtToken(request.Phone, "User", _config);
                    return Ok(new { token, UserPermission = "Y", UserId = userId });
                }
                else if (partyName == null && userId > 0)
                {
                    var token = JwtHelper.GenerateJwtToken(request.Phone, "User", _config);
                    return Ok(new { token, UserPermission = "N", UserId = userId });
                }

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
                var connectionString = DBHelper.GetConnection();
                using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();

                // ✅ Updated: Select more columns
                var cmd = new SqlCommand(@"
            SELECT TOP 1 FCOMPCODE, FADMIN, PHONE1
            FROM COMPANY 
            WHERE FADMIN = @username AND FSUP = @password", connection);

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
                        UserPermission = "N",
                        UserId = fcompcode,
                        AdminName = adminName,
                        Phone = phone
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








    }
}

public class LoginRequest
{
    public string Phone { get; set; }
}