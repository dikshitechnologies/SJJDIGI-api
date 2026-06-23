
using CHITSCHEME.Helpers;
using CHITSCHEME.Models;
using JEWELLBISREACT.DBConnection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.IdentityModel.Tokens.Jwt;
namespace CHITSCHEME.Controllers.Jewellery
{
    [Route("api/[controller]")]
    [ApiController]
    public class RegisterPage2Controller : ControllerBase
    {

        //---------------------------------------------Duplicate Name Checking ---------------------------------
        private bool RegistruserExists(SqlConnection con, string sectionName)
        {
            using (SqlCommand cmd = new SqlCommand("SELECT 1 FROM RegisterUsers  where PhoneNumber=@PhoneNumber", con))
            {
                cmd.Parameters.AddWithValue("@PhoneNumber", sectionName);
                return cmd.ExecuteScalar() != null;
            }
        }



        [HttpPost("RegisterUser")]
        public async Task<IActionResult> RegisterUser([FromBody] RegisterUser model)
        {

            if (model == null  )
            {
                return BadRequest("Model cannot be null.");
            }

            // Validate input
            if (string.IsNullOrWhiteSpace(model.Firstname) || model.Firstname.ToLower() == "string")
            {
                return BadRequest("First name is empty.");
            }
            if (model.Firstname.Length > 100)
            {
                return BadRequest("First name cannot exceed 100 characters.");
            }


            if (string.IsNullOrWhiteSpace(model.Phonenumber) || model.Phonenumber.ToLower() == "string")
            {
                return BadRequest("Phone number is empty.");
            }
            if (model.Phonenumber.Length > 20)
            {
                return BadRequest("Phone number cannot exceed 20 characters.");
            }

            using(SqlConnection connection = new SqlConnection(DBHelper.GetConnection()))
            {
                await connection.OpenAsync();

                using (SqlCommand cmd = new SqlCommand("SELECT COUNT(*) FROM REGISTERUSERS WHERE PHONENUMBER=@PHONENUMBER", connection))
                {
                    cmd.Parameters.AddWithValue("PHONENUMBER", model.Phonenumber);
                    int count = (int)await cmd.ExecuteScalarAsync();
                    if (count > 0)
                    {
                        return BadRequest(new { message = "Phone number already exists." });
                    }
                }
            }
           

                string maxRegisterIdQuery = "SELECT MAX(UserID) FROM RegisterUsers";
            string insertQuery = @"
        INSERT INTO RegisterUsers (UserID,UserName, Email, PhoneNumber, PasswordHash,CreatedAt, FcmToken, DeviceType, LastLogin)
        VALUES (@UserID,@UserName, @Email, @PhoneNumber, @PasswordHash,@CreatedAt, @FcmToken, @DeviceType, @LastLogin);";

            using (SqlConnection conn = new SqlConnection(DBHelper.GetConnection()))
            {
                try
                {
                    // Open connection
                    await conn.OpenAsync();

                    // Get the last user id from the database
                    using (SqlCommand maxIdCommand = new SqlCommand(maxRegisterIdQuery, conn))
                    {
                        object result = await maxIdCommand.ExecuteScalarAsync();

                        string newUserCode;
                        if (result == DBNull.Value || result == null)
                        {
                            newUserCode = "1000";  // First user
                        }
                        else
                        {
                            string lastUserId = result.ToString();
                            if (int.TryParse(lastUserId, out int lastId))
                            {
                                int nextId = lastId + 1;
                                newUserCode = nextId.ToString("D4");  
                            }
                            else
                            {
                                return StatusCode(500, new { message = "Invalid user ID format in database." });
                            }
                        }

                        if (RegistruserExists(conn, model.Phonenumber))
                        {
                            return Conflict(new { message = "Phonenumber  already exists" });
                        }
                        // Insert new user with the generated user code
                        using (SqlCommand cmd = new SqlCommand(insertQuery, conn))
                        {


                            cmd.Parameters.AddWithValue("@UserID", newUserCode);
                            cmd.Parameters.AddWithValue("@UserName", model.Firstname);
                            cmd.Parameters.AddWithValue("@Email", model.Email);
                            cmd.Parameters.AddWithValue("@PhoneNumber", model.Phonenumber);
                            cmd.Parameters.AddWithValue("@PasswordHash", "");
                            cmd.Parameters.AddWithValue("@CreatedAt", DateTime.Now);
                            cmd.Parameters.AddWithValue("@FcmToken", (object)model.FcmToken ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@DeviceType", (object)model.DeviceType ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@LastLogin", DateTime.Now);
                            int rowsAffected = await cmd.ExecuteNonQueryAsync();

                            if (rowsAffected > 0)
                            {
                                return Ok(new { message = "User registered successfully" });
                            }
                            else
                            {
                                return StatusCode(500, "Failed to register user.");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Log the exception
                    return StatusCode(500, new { message = $"An error occurred: {ex.Message}" });
                }
                finally
                {
                    // Ensure that the connection is closed
                    conn.Close();
                }
            }
        }




        [HttpGet("profilePage/{UserID}")]
        public IActionResult ProfilePage(string UserID)
        {
            try
            {
                var token = Request.Headers["Authorization"].ToString()
                   .Replace("Bearer ", "", StringComparison.OrdinalIgnoreCase)
                   .Trim();

                if (string.IsNullOrWhiteSpace(token))
                    return Unauthorized("Token is missing or invalid.");


                if (!new JwtSecurityTokenHandler().CanReadToken(token))
                    return Unauthorized("Malformed JWT.");

                string role = JwtHelper.GetRoleFromJwtToken(token);

                if (string.IsNullOrEmpty(role))
                    return Unauthorized(new { message = "Invalid or expired token" });

                using (SqlConnection connection = new SqlConnection(DBHelper.GetConnection()))
                {
                    connection.Open();
                    string query;

                    if (role == "Admin")
                    {
                        // Admin → COMPANY table (no AddressLine, City, etc.)
                        query = @"
                    SELECT 
                        fcompname AS UserName, 
                        PHONE1 AS Email,
                        '' AS AddressLine,
                        '' AS City,
                        '' AS State,
                        '' AS Pincode,
                        '' AS fprofileImg
                    FROM COMPANY
                    WHERE fcompcode = @UserID";
                    }
                    else
                    {
                        // Normal user → RegisterUsers table
                        query = @"
                    SELECT 
                        UserName, 
                        Email, 
                        ISNULL(AddressLine, '') AS AddressLine,
                        ISNULL(City, '') AS City,
                        ISNULL(State, '') AS State,
                        ISNULL(Pincode, '') AS Pincode,
                        ISNULL(fprofileImg, '') AS fprofileImg
                    FROM RegisterUsers 
                    WHERE UserID = @UserID";
                    }

                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@UserID", UserID);

                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                var result = new
                                {
                                    UserName = reader["UserName"].ToString(),
                                    Email = reader["Email"].ToString(),
                                    AddressLine = reader["AddressLine"].ToString(),
                                    City = reader["City"].ToString(),
                                    State = reader["State"].ToString(),
                                    Pincode = reader["Pincode"].ToString(),
                                    fprofileImg = reader["fprofileImg"].ToString()
                                };

                                return Ok(result);
                            }
                            else
                            {
                                return NotFound(new { message = "User not found" });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal Server Error: {ex.Message}");
            }
        }


    }
}
