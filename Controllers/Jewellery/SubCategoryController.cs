using System.Text.Json.Serialization;
using JEWELLBISREACT.DBConnection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace CHITSCHEME.Controllers.Jewellery
{
    [Route("api/[controller]")]
    [Authorize]
    [ApiController]
    public class SubCategoryController : ControllerBase
    {

        [HttpGet("subcategory/{categoryCode}")]
        public IActionResult GetSubCategoryItems([FromRoute] string categoryCode,[FromQuery] int pageNumber = 1,[FromQuery] int pageSize = 20)
        {
            List<SubCategoryItem> items = new List<SubCategoryItem>();

            string connectionString = DBHelper.GetConnection();

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                try
                {
                    conn.Open();

                    string query = @"
                SELECT fItemcode, fItemName, fimage 
                FROM item11 
                WHERE fParent LIKE 
                    (SELECT fParent FROM item WHERE fitemcode = @categoryCode) + '%' 
                  AND fAclevel = 3
                ORDER BY fItemcode 
                OFFSET @Offset ROWS 
                FETCH NEXT @PageSize ROWS ONLY";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@categoryCode", categoryCode);
                        cmd.Parameters.AddWithValue("@Offset", (pageNumber - 1) * pageSize);
                        cmd.Parameters.AddWithValue("@PageSize", pageSize);

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                items.Add(new SubCategoryItem
                                {
                                    FCode = reader["fItemcode"].ToString(),
                                    SubCategoryName = reader["fItemName"].ToString(),
                                    Image = reader["fimage"]?.ToString()
                                });
                            }
                        }
                    }

                    return Ok(new{data = items});
                }
                catch (SqlException)
                {
                    return StatusCode(500, new { error = "Database error occurred." });
                }
                catch (Exception)
                {
                    return StatusCode(500, new { error = "Unexpected error occurred." });
                }
            }
        }

    }
}


public class SubCategoryItem
{
    [JsonPropertyName("code")]
    public string FCode { get; set; }

    [JsonPropertyName("name")]
    public string SubCategoryName { get; set; }

    [JsonPropertyName("image")]
    public string Image { get; set; }
}