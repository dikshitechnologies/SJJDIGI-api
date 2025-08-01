using System.Text.Json.Serialization;
using CHITSCHEME.Global;
using JEWELLBISREACT.DBConnection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace CHITSCHEME.Controllers.Jewellery
{
    [Route("api/[controller]")]
    [Authorize]
    [ApiController]
    public class ListItemsController : ControllerBase
    {


        //------------------------------------------------All Category List Items   Mixed --------------------
        [HttpGet]
        [Route("ItemsList/{parent}")]
        public async Task<IActionResult> ItemsList(
       [FromRoute] string parent,
       [FromQuery] int pageNumber = 1,
       [FromQuery] int pageSize = 20,
       [FromQuery] string searchTerm = "", [FromQuery] string customerCode="")
        {   

            if (string.IsNullOrWhiteSpace(customerCode))
            {
                return BadRequest(new { message = "User Id is required" });
            }

            if (pageNumber <= 0) pageNumber = 1;
            if (pageSize <= 0 || pageSize > 100) pageSize = 20;

            List<ListAllItem> itemsList = new List<ListAllItem>();
            int totalRecords = 0;

            try
            {
                using (SqlConnection connection = new SqlConnection(DBHelper.GetConnection()))
                {
                    await connection.OpenAsync();

                    string baseCondition = "i.fAclevel = -4 AND (i.fparent LIKE @fparent)";
                    if (!string.IsNullOrWhiteSpace(searchTerm))
                    {
                        baseCondition += " AND (i.fItemName LIKE @search OR i.fDesignNo LIKE @search OR i.fItemcode LIKE @search)";
                    }

                    string countQuery = $@"
                SELECT COUNT(*) 
                FROM Item11 i
                INNER JOIN Division d ON i.fPurity = d.fName
                WHERE {baseCondition}";

                    using (SqlCommand countCommand = new SqlCommand(countQuery, connection))
                    {
                        countCommand.Parameters.AddWithValue("@fparent", parent + "%");
                        if (!string.IsNullOrWhiteSpace(searchTerm))
                        {
                            countCommand.Parameters.AddWithValue("@search", "%" + searchTerm + "%");
                        }

                        totalRecords = (int)await countCommand.ExecuteScalarAsync();
                    }

                    string query = $@"
                SELECT 
                    i.fItemcode,
                    i.fparent,
                    i.fItemName, 
                    i.fDesignNo, 
                    CASE WHEN w.fProductCode IS NOT NULL THEN 'Y' ELSE 'N' END AS IsWishlist,
                    i.fimage, 
                    i.Weight,
                    i.NetWt,
                    i.fVA, 
                    i.fVAGMS, 
                    i.fMc, 
                    i.fOthers, 
                    i.fTax, 
                    i.fStoneCharges, 
                    i.fPieceRate, 
                    i.fRate,
                    d.fRate AS GoldRate
               FROM Item11 i
                INNER JOIN Division d ON i.fPurity = d.fName
                LEFT JOIN Wishlist w ON i.fItemcode = w.fProductCode AND w.fCusCode = @customerCode
                WHERE {baseCondition}
                ORDER BY i.fItemcode
                OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        int offset = (pageNumber - 1) * pageSize;
                        command.Parameters.AddWithValue("@fparent", parent + "%");
                        command.Parameters.AddWithValue("@Offset", offset);
                        command.Parameters.AddWithValue("@PageSize", pageSize);
                        command.Parameters.AddWithValue("@customerCode", customerCode);
                        if (!string.IsNullOrWhiteSpace(searchTerm))
                        {
                            command.Parameters.AddWithValue("@search", "%" + searchTerm + "%");
                        }

                        using (SqlDataReader reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                string pieceRate = reader["fPieceRate"]?.ToString();
                                decimal weight = SafeGetDecimal(reader, "Weight");
                                decimal NetWt = SafeGetDecimal(reader, "NetWt");
                                decimal vaPercent = SafeGetDecimal(reader, "fVA");
                                decimal vaGrams = SafeGetDecimal(reader, "fVAGMS");
                                decimal mc = SafeGetDecimal(reader, "fMc");
                                decimal others = SafeGetDecimal(reader, "fOthers");
                                decimal stoneCharges = SafeGetDecimal(reader, "fStoneCharges");
                                decimal taxPercent = SafeGetDecimal(reader, "fTax");
                                decimal goldRate = SafeGetDecimal(reader, "GoldRate");
                                decimal fRate = SafeGetDecimal(reader, "fRate");

                                decimal totalItemPrice = 0;
                                if (pieceRate == "Y")
                                {
                                    totalItemPrice = fRate + mc + others + stoneCharges;
                                }
                                else
                                {
                                    decimal totalWastage = (vaGrams > 0) ? vaGrams : (NetWt * vaPercent / 100);
                                    decimal totalWeightWithWastage = NetWt + totalWastage;
                                    totalItemPrice = (totalWeightWithWastage * goldRate) + mc + others + stoneCharges;
                                }

                                decimal taxAmount = (taxPercent > 0) ? (totalItemPrice * taxPercent / 100) : 0;
                                totalItemPrice += taxAmount;

                                itemsList.Add(new ListAllItem
                                {
                                    ItemCode = reader["fItemcode"]?.ToString() ?? "",
                                    ItemName = reader["fItemName"]?.ToString() ?? "",
                                    IsWishlist = reader["IsWishlist"]?.ToString() ?? "N",
                                    fparent = reader["fparent"]?.ToString() ?? "",
                                    Image = reader["fimage"]?.ToString() ?? "",
                                    TotalPrice = totalItemPrice
                                });
                            }
                        }
                    }
                }

                var paginationResponse = new
                {
                    totalRecords,
                    totalPages = (int)Math.Ceiling(totalRecords / (double)pageSize),
                    pageNumber,
                    pageSize,
                    items = itemsList
                };

                return Ok(paginationResponse);
            }
            catch (SqlException sqlEx)
            {
                return StatusCode(500, new { error = "Database error occurred.", details = sqlEx.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Unexpected error occurred.", details = ex.Message });
            }
        }

        private decimal SafeGetDecimal(SqlDataReader reader, string columnName)
        {
            object value = reader[columnName];
            if (value == DBNull.Value || string.IsNullOrWhiteSpace(value.ToString()))
                return 0;

            return Convert.ToDecimal(value);
        }


        [HttpGet("SubCategorys/{parentCode}")]
        public async Task<IActionResult> SubCtegorys([FromRoute] string parentCode ,[FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20)
        {
            var subItemList = new List<SubcategoryItems>();
       
            try
            {
                using (SqlConnection conn = new SqlConnection(DBHelper.GetConnection()))
                {
                    await conn.OpenAsync();

                    string query = @"
                SELECT 
                    i.fItemcode, 
                    i.fParent,
                    i.fItemName, 
                    i.fimage
                FROM Item11 i
                WHERE i.fAclevel = 3 AND 
                      i.fparent LIKE @parentPrefix
                ORDER BY i.fItemcode
                OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@parentPrefix", parentCode+"%");
                        cmd.Parameters.AddWithValue("@Offset", (pageNumber - 1) * pageSize);
                        cmd.Parameters.AddWithValue("@PageSize", pageSize);

                        using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                subItemList.Add(new SubcategoryItems
                                {
                                    ItemCode = reader["fItemcode"]?.ToString(),
                                    Fparent = reader["fParent"]?.ToString(),
                                    ItemName = reader["fItemName"]?.ToString(),
                                    Fimage = reader["fimage"]?.ToString()
                                });
                            }
                        }
                    }
                }
                subItemList.Insert(0, new SubcategoryItems
                {
                    ItemCode = "", 
                    Fparent = parentCode,
                    ItemName = "All",
                    Fimage = "" 
                });
                return Ok(subItemList);
            }
            catch (SqlException sqlEx)
            {
                return StatusCode(500, new { error = "Database error occurred.", details = sqlEx.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Unexpected error occurred.", details = ex.Message });
            }
        }


        public class SubcategoryItems{
            public string  ItemCode { get; set; }
            public string ItemName { get; set; } = Empty.ToString();
            public string Fparent { get; set; }
            public string Fimage { get; set; }
        }



        //------------------------------------------------ Items Details ------------------------------------
        [HttpGet]
        [Route("itemDetails/{parentCode}")]
        public async Task<IActionResult> itemDetails([FromRoute] string parentCode, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, [FromQuery] string customerCode = "")
        {
            List<ListAllItem> ItemsList = new List<ListAllItem>();

            try
            {
                using (SqlConnection connection = new SqlConnection(DBHelper.GetConnection()))
                {
                    await connection.OpenAsync();

                    string query = @"
            SELECT 
                i.fItemcode,
                i.fParent,
                i.fItemName,
                i.fDesignNo,
                i.fimage,
                i.fPurity,
                i.Color,
                i.size,
                i.Weight,
                i.NetWt,
                i.fGrossWt,
                i.LessWt,
                i.fVA,
                i.fVAGMS,
                i.fMc,
                i.fOthers,
                i.fTax,
                i.fStoneCharges,
                i.fimage2,
                i.fimage3,
                i.fimage4,
                i.fPieceRate,
                i.fRate,
                d.fRate AS GoldRate,
                i.fdescription,
                CASE WHEN w.fProductCode IS NOT NULL THEN 'Y' ELSE 'N' END AS IsWishlist
            FROM 
                Item11 i
            LEFT JOIN 
                Division d ON i.fPurity = d.fName
            LEFT JOIN 
            Wishlist w ON i.fItemcode = w.fProductCode AND w.fCusCode = @customerCode
            WHERE 
                (i.fparent =@fparent)
            ORDER BY i.fItemcode
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        int offset = (pageNumber - 1) * pageSize;
                        command.Parameters.AddWithValue("@fparent", parentCode);
                        command.Parameters.AddWithValue("@Offset", offset);
                        command.Parameters.AddWithValue("@PageSize", pageSize);
                        command.Parameters.AddWithValue("@customerCode", customerCode);

                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                string pieceRate = reader["fPieceRate"]?.ToString();
                                decimal baseWeight = SafeGetDecimal(reader, "Weight");
                                decimal netWt = SafeGetDecimal(reader, "NetWt");
                                decimal lessWt = SafeGetDecimal(reader, "LessWt");
                                decimal fGrossWt = SafeGetDecimal(reader, "fGrossWt");
                                decimal fVA = SafeGetDecimal(reader, "fVA");
                                decimal fVAGMS = SafeGetDecimal(reader, "fVAGMS");
                                decimal fMc = SafeGetDecimal(reader, "fMc");
                                decimal fStoneCharges = SafeGetDecimal(reader, "fStoneCharges");
                                decimal fTax = SafeGetDecimal(reader, "fTax");
                                decimal fOthers = SafeGetDecimal(reader, "fOthers");
                                decimal goldRate = SafeGetDecimal(reader, "GoldRate");
                                decimal fRate = SafeGetDecimal(reader, "fRate");

                                var result = PriceCalculator.CalculatePrice(pieceRate, netWt, fVA, fVAGMS, fRate, fMc, fOthers, fStoneCharges, fTax, goldRate);
                                decimal totalAmount = result.TotalAmount;
                                decimal taxAmount = result.TaxAmount;

                                var productDetails = new
                                {
                                    fItemcode = reader["fItemcode"]?.ToString() ?? null,
                                    fItemName = reader["fItemName"]?.ToString() ?? null,
                                    fDesignNo = reader["fDesignNo"]?.ToString() ?? null,
                                    fimage = reader["fimage"]?.ToString() ?? string.Empty,
                                    fPurity = reader["fPurity"]?.ToString() ?? null,
                                    Color = reader["Color"]?.ToString() ?? null,
                                    Size = reader["size"]?.ToString() ?? null,
                                    fParent = reader["fParent"]?.ToString() ?? null,
                                    BaseWeight = baseWeight,
                                    NetWt = netWt,
                                    LessWt = lessWt,
                                    fGrossWt = fGrossWt,
                                    fVA = fVA,
                                    fVAGMS = fVAGMS,
                                    TotalWastage = result.TotalWastage,
                                    TotalWeightWithWastage = result.TotalWeightWithWastage,
                                    GoldRate = goldRate,
                                    TodayRate = result.TodayRate,
                                    fMc = fMc,
                                    TaxAmount = taxAmount,
                                    fTax = fTax,
                                    fOthers = fOthers,
                                    fStoneCharges = fStoneCharges,
                                    fimage2 = reader["fimage2"]?.ToString() ?? string.Empty,
                                    fimage3 = reader["fimage3"]?.ToString() ?? string.Empty,
                                    fimage4 = reader["fimage4"]?.ToString() ?? string.Empty,
                                    TotalAmount = totalAmount,
                                    fdescription = reader["fdescription"]?.ToString() ?? "Description not available",
                                    IsWishlist = reader["IsWishlist"]?.ToString() ?? "N",
                                };

                                return Ok(productDetails);
                            }
                        }
                    }
                }

                return Ok(new { items = ItemsList });
            }
            catch (SqlException sqlEx)
            {
                return StatusCode(500, new { error = "Database error occurred.", details = sqlEx.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Unexpected error occurred.", details = ex.Message });
            }
        }

        [HttpGet]
        [Route("SearchItems/{parentCode}")]
        public async Task<IActionResult> SearchItems([FromRoute] string parentCode, [FromQuery] string searchText = "", [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20)
        {
            List<ListAllItem> SearchItems = new List<ListAllItem>();

            try
            {
                using (SqlConnection connection = new SqlConnection(DBHelper.GetConnection()))
                {
                    await connection.OpenAsync();

                    string query = @"
                SELECT 
                    I.FITEMCODE,
            I.FPARENT,
            I.FITEMNAME, 
            I.FIMAGE, 
            I.fPieceRate, 
            I.fRate,  
            I.Weight, 
            i.NetWt,
            i.fGrossWt,
            i.LessWt,
            i.fVA,
            i.fVAGMS,
            i.fMc,
            i.fOthers,
            i.fTax,
            i.fStoneCharges,
            i.fimage2,
            i.fimage3,
            i.fimage4,
            i.fPieceRate,
            i.fRate,
            d.fRate AS GoldRate,
            I.NetWt, 
            i.LessWt,
            I.fVA, 
            I.fVAGMS, 
            I.fMc, 
            I.fOthers, 
            I.fStoneCharges, 
            D.fRate AS GoldRate
                FROM Item11 i
                INNER JOIN Division d ON i.fPurity = d.fName
                WHERE i.fAclevel < 0 AND 
                      i.fParent LIKE @fParent AND
                      (
                        i.fItemName LIKE @search OR 
                        i.fItemcode LIKE @search OR 
                        i.fDesignNo LIKE @search
                      )
                ORDER BY i.fItemcode
                OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        int offset = (pageNumber - 1) * pageSize;
                        command.Parameters.AddWithValue("@search", "%" + searchText + "%");
                        command.Parameters.AddWithValue("@fParent", parentCode + "%");
                        command.Parameters.AddWithValue("@Offset", offset);
                        command.Parameters.AddWithValue("@PageSize", pageSize);

                        using (SqlDataReader reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                string pieceRate = reader["fPieceRate"]?.ToString();
                                decimal baseWeight = SafeGetDecimal(reader, "Weight");
                                decimal netWt = SafeGetDecimal(reader, "NetWt");
                                decimal lessWt = SafeGetDecimal(reader, "LessWt");
                                decimal fGrossWt = SafeGetDecimal(reader, "fGrossWt");
                                decimal fVA = SafeGetDecimal(reader, "fVA");
                                decimal fVAGMS = SafeGetDecimal(reader, "fVAGMS");
                                decimal fMc = SafeGetDecimal(reader, "fMc");
                                decimal fStoneCharges = SafeGetDecimal(reader, "fStoneCharges");
                                decimal fTax = SafeGetDecimal(reader, "fTax");
                                decimal fOthers = SafeGetDecimal(reader, "fOthers");
                                decimal goldRate = SafeGetDecimal(reader, "GoldRate");
                                decimal fRate = SafeGetDecimal(reader, "fRate");

                                var result = PriceCalculator.CalculatePrice(pieceRate, netWt, fVA, fVAGMS, fRate, fMc, fOthers, fStoneCharges, fTax, goldRate);
                                decimal totalAmount = result.TotalAmount;

                                SearchItems.Add(new ListAllItem
                                {
                                    ItemCode = reader["fItemcode"]?.ToString() ?? "",
                                    ItemName = reader["fItemName"]?.ToString() ?? "",
                                    Image = reader["fimage"]?.ToString() ?? "",
                                    TotalPrice = totalAmount
                                });
                            }
                        }
                    }
                }

                return Ok(new { items = SearchItems });
            }
            catch (SqlException sqlEx)
            {
                return StatusCode(500, new { error = "Database error occurred.", details = sqlEx.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Unexpected error occurred.", details = ex.Message });
            }
        }

        //----------------------------------------------Hompage NewArrivals 20 Items -------------------------------------------

        [HttpGet("NewArrivals")]
        public async Task<IActionResult> NewArrivals()
        {
            try
            {
                var items = new List<JewelleryItem>();

                string query = @"
        SELECT TOP 20 
            I.FITEMCODE,
            I.FPARENT,
            I.FITEMNAME, 
            I.FIMAGE, 
            I.fPieceRate, 
            I.fRate,  
            I.Weight, 
            i.NetWt,
            i.fGrossWt,
            i.LessWt,
            i.fVA,
            i.fVAGMS,
            i.fMc,
            i.fOthers,
            i.fTax,
            i.fStoneCharges,
            i.fimage2,
            i.fimage3,
            i.fimage4,
            i.fPieceRate,
            i.fRate,
            d.fRate AS GoldRate,
            I.NetWt, 
            i.LessWt,
            I.fVA, 
            I.fVAGMS, 
            I.fMc, 
            I.fOthers, 
            I.fStoneCharges, 
            D.fRate AS GoldRate
        FROM ITEM11 I 
        JOIN DIVISION D ON D.fName = I.fPurity  
        WHERE fAclevel < '0' 
        ORDER BY FITEMCODE DESC";

                using (SqlConnection connection = new SqlConnection(DBHelper.GetConnection()))
                {
                    await connection.OpenAsync();

                    using (SqlCommand command = new SqlCommand(query, connection))
                    using (SqlDataReader reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            string pieceRate = reader["fPieceRate"]?.ToString();
                            decimal baseWeight = SafeGetDecimal(reader, "Weight");
                            decimal netWt = SafeGetDecimal(reader, "NetWt");
                            decimal lessWt = SafeGetDecimal(reader, "LessWt");
                            decimal fGrossWt = SafeGetDecimal(reader, "fGrossWt");
                            decimal fVA = SafeGetDecimal(reader, "fVA");
                            decimal fVAGMS = SafeGetDecimal(reader, "fVAGMS");
                            decimal fMc = SafeGetDecimal(reader, "fMc");
                            decimal fStoneCharges = SafeGetDecimal(reader, "fStoneCharges");
                            decimal fTax = SafeGetDecimal(reader, "fTax");
                            decimal fOthers = SafeGetDecimal(reader, "fOthers");
                            decimal goldRate = SafeGetDecimal(reader, "GoldRate");
                            decimal fRate = SafeGetDecimal(reader, "fRate");

                            var result = PriceCalculator.CalculatePrice(pieceRate, netWt, fVA, fVAGMS, fRate, fMc, fOthers, fStoneCharges, fTax, goldRate);
                            decimal totalAmount = result.TotalAmount;

                            items.Add(new JewelleryItem
                            {
                                ItemCode = reader["FITEMCODE"].ToString(),
                                fparent = reader["FPARENT"].ToString(),
                                Name = reader["FITEMNAME"].ToString(),
                                Image = reader["FIMAGE"] != DBNull.Value ? reader["FIMAGE"].ToString() : null,
                                Price = totalAmount
                            });
                        }
                    }
                }

                return Ok(items);
            }
            catch (SqlException sqlEx)
            {
                return StatusCode(500, new { error = "Database error occurred.", details = sqlEx.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Unexpected error occurred.", details = ex.Message });
            }
        }


        //-----------------------------------Selected List Items ----------------------------------------




    }
}




public class JewelleryItem
{
    [JsonPropertyName("itemCode")]
    public string ItemCode { get; set; }    
    [JsonPropertyName("fparent")]
    public string fparent { get; set; }
    [JsonPropertyName("itemName")]
    public string Name { get; set; }
    [JsonPropertyName("image")]
    public string Image { get; set; }

    [JsonPropertyName("totalPrice")]
    public decimal Price { get; set; }
}


public class ListAllItem
{
    [JsonPropertyName("itemCode")]
    public string ItemCode { get; set; }

    [JsonPropertyName("itemName")]
    public string ItemName { get; set; }    
    [JsonPropertyName("fparent")]
    public string fparent { get; set; }

    [JsonPropertyName("image")]
    public string Image { get; set; }

    [JsonPropertyName("totalPrice")]
    public decimal TotalPrice { get; set; }
    
    [JsonPropertyName("isWishlist")]
    public string IsWishlist { get; set; }

}

