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
        public IActionResult GetSubCategoryItems(
            [FromRoute] string categoryCode,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20)
        {
            List<SubCategoryItem> items = new List<SubCategoryItem>();
            int totalCount = 0;

            string connectionString = DBHelper.GetConnection();

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                try
                {
                    conn.Open();

                    // Count total records for pagination
                    string countQuery = @"
                SELECT COUNT(*) 
                FROM item i
                WHERE i.fParent LIKE (SELECT fParent FROM item WHERE fitemcode = @categoryCode) + '%'
                  AND i.fAclevel < 0;";

                    using (SqlCommand countCmd = new SqlCommand(countQuery, conn))
                    {
                        countCmd.Parameters.AddWithValue("@categoryCode", categoryCode);
                        totalCount = (int)countCmd.ExecuteScalar();
                    }

                    // Main paginated query
                    string query = @"
                SELECT 
                    i.fItemcode,
                    i.fItemName,
                    COALESCE(
                        (SELECT TOP 1 
                             COALESCE(op.FIMAGE1, op.FIMAGE2, op.FIMAGE3, op.FIMAGE4) 
                         FROM ITEMPURCHASEOP op
                         WHERE op.Itemcode = i.fItemcode
                         ORDER BY op.FDATE DESC),
                        i.fImage
                    ) AS FinalImage,
                    (SELECT TOP 1 op.FDATE 
                     FROM ITEMPURCHASEOP op
                     WHERE op.Itemcode = i.fItemcode
                     ORDER BY op.FDATE DESC) AS LastPurchaseDate
                FROM item i
                WHERE i.fParent LIKE (SELECT fParent FROM item WHERE fitemcode = @categoryCode) + '%'
                  AND i.fAclevel < 0
                ORDER BY i.fItemcode
                OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;";

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
                                    Image = reader["FinalImage"]?.ToString(),

                                });
                            }
                        }
                    }

                    return Ok(new
                    {
                        data = items,
                        pagination = new
                        {
                            pageNumber,
                            pageSize,
                            totalRecords = totalCount,
                            totalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
                        }
                    });
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