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
                string userId = string.Empty;

                var regDetailsCmd = new SqlCommand(
                    "SELECT fcode, fAcname, fMail FROM party WHERE fparent like '000020000900015%' and fPhone= @phone",
                    connection);
                regDetailsCmd.Parameters.AddWithValue("@phone", request.Phone);

                using (var reader = await regDetailsCmd.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        userId = reader["fcode"].ToString();
                        username = reader["fAcname"].ToString();
                        email = reader["fMail"].ToString();
                    }
                }

      
                // -------- If party exists and already registered --------
                if ((partyName != null && partyPhone != null) && userId != "")
                {
                    var token = JwtHelper.GenerateJwtToken(request.Phone, "User", _config);
                    return Ok(new { token, UserPermission = "U", UserId = userId, username, email, phone=partyPhone });
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
                        UserPermission = "A",
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

        [AllowAnonymous]
        [HttpPost("guest-login")]
        public IActionResult GuestLogin()
        {
            try
            {
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
                    email = ""
                });
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Error creating guest login.", error = ex.Message });
            }
        }







    }
}

public class LoginRequest
{
    public string Phone { get; set; }
}