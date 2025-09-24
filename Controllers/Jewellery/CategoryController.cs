using System.Text.Json.Serialization;
using JEWELLBISREACT.DBConnection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace CHITSCHEME.Controllers.Jewellery
{
    [Route("api/[controller]")]
    [Authorize(Roles = "Guest,User,Admin")]
    [ApiController]
    public class CategoryController : ControllerBase
    {

        [HttpGet("categories")]
        public IActionResult GetCategories()
        {
            string connectionString = DBHelper.GetConnection();
            List<CategoryItem> categories = new List<CategoryItem>();


            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                try
                {
                    conn.Open();
                    string query = "SELECT fItemcode,fparent, fItemName, fimage FROM Item WHERE LEFT(fParent, 5) = '00001' AND fAclevel = 2 and flag ='Y'";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            categories.Add(new CategoryItem
                            {
                                FCode = reader["fItemcode"].ToString(),
                                fparent = reader["fparent"].ToString(),
                                Name = reader["fItemName"].ToString(),
                                Image = reader["fimage"]?.ToString()
                            });
                        }
                    }

                    return Ok(categories);
                }
                catch (SqlException sqlEx)
                {
                    return StatusCode(500, new
                    {
                        error = "A database error occurred."
                    });
                }
                catch (Exception ex)
                {
                    return StatusCode(500, new
                    {
                        error = "An unexpected error occurred."
                    });
                }
            }
        }

    }
}



public class CategoryItem
{
    [JsonPropertyName("code")]
    public string FCode { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }
    [JsonPropertyName("fparent")]
    public string fparent { get; set; }

    [JsonPropertyName("image")]
    public string Image { get; set; }
}
