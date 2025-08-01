using CHITSCHEME.Global;
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
    public class WishListController : ControllerBase
    {

        [HttpPost("AddToWishlist")]
        public async Task<IActionResult> AddToCart([FromBody] Wishlist wishlist)
        {
            if (wishlist == null || string.IsNullOrEmpty(wishlist.ProductCode))
            {
                return BadRequest(new { message = "Invalid Wishlist data. Product code is required." });
            }

            try
            {
                using (SqlConnection connection = new SqlConnection(DBHelper.GetConnection()))
                {
                    await connection.OpenAsync();

                    string checkProductQuery = "SELECT COUNT(*) FROM Wishlist WHERE fCusCode = @fCusCode AND fProductCode = @productCode";
                    using (SqlCommand checkCommand = new SqlCommand(checkProductQuery, connection))
                    {
                        checkCommand.Parameters.AddWithValue("@fCusCode", wishlist.CusCode);
                        checkCommand.Parameters.AddWithValue("@productCode", wishlist.ProductCode);

                        int existingCount = (int)await checkCommand.ExecuteScalarAsync();
                        if (existingCount > 0)
                        {
                            return BadRequest(new { message = "You have already added this product to your Wishlist." });
                        }
                    }

                    string maxCartIdQuery = "SELECT MAX(fWishListId) FROM Wishlist";
                    using (SqlCommand maxIdCommand = new SqlCommand(maxCartIdQuery, connection))
                    {
                        object result = await maxIdCommand.ExecuteScalarAsync();

                        string newWishlistId;
                        if (result == DBNull.Value || result == null)
                        {
                            newWishlistId = "00001";
                        }
                        else
                        {
                            string lastWishlistCode = result.ToString();
                            if (int.TryParse(lastWishlistCode, out int lastCode))
                            {
                                int nextId = lastCode + 1;
                                newWishlistId = nextId <= 99999 ? nextId.ToString("D5") : nextId.ToString();
                            }
                            else
                            {
                                return StatusCode(500, new { error = "Invalid cart ID format in database." });
                            }
                        }

                        string insertQuery = "INSERT INTO Wishlist (fWishListId, fCusCode, fProductCode, fWdate) " +
                                             "VALUES (@fWishListId, @fCusCode, @fProductCode, @fWdate)";

                        using (SqlCommand insertCommand = new SqlCommand(insertQuery, connection))
                        {
                            insertCommand.Parameters.AddWithValue("@fWishListId", newWishlistId);
                            insertCommand.Parameters.AddWithValue("@fCusCode", wishlist.CusCode);
                            insertCommand.Parameters.AddWithValue("@fProductCode", wishlist.ProductCode);
                            insertCommand.Parameters.AddWithValue("@fWdate", DateTime.Now);

                            int insertResult = await insertCommand.ExecuteNonQueryAsync();

                            if (insertResult > 0)
                            {
                                return Ok(new { message = "Item added to Wishlist successfully.", wishlistCode = newWishlistId });
                            }
                            else
                            {
                                return StatusCode(500, new { message = "Failed to add item to cart." });
                            }
                        }
                    }
                }
            }
            catch (SqlException sqlEx)
            {
                return StatusCode(500, new { error = "Database error occurred.", details = sqlEx.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "An unexpected error occurred.", details = ex.Message });
            }
        }

        [HttpGet("WishlistItemCount/{fCusid}")]
        public async Task<IActionResult> GetCartItemCount([FromRoute] string fCusid)
        {
            try
            {
                int count = 0;

                using (SqlConnection conn = new SqlConnection(DBHelper.GetConnection()))
                {
                    await conn.OpenAsync();

                    string query = "SELECT COUNT(*) FROM Wishlist WHERE fCusCode = @fCusid";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@fCusid", fCusid);

                        count = (int)await cmd.ExecuteScalarAsync();
                    }
                }

                return Ok(new { cartItemCount = count });
            }
            catch (SqlException sqlEx)
            {
                return StatusCode(500, new { message = "Database error occurred.", details = sqlEx.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Unexpected error occurred.", details = ex.Message });
            }
        }






        [HttpGet]
        [Route("WishlistViewItem")]
        public async Task<IActionResult> GetWishlistItems(string fCusCode)
        {
            List<WishlistItem> wishlistItems = new List<WishlistItem>();


            try
            {
                using (SqlConnection connection = new SqlConnection(DBHelper.GetConnection()))
                {
                    await connection.OpenAsync();

                    string query = @"
                        SELECT 
                     C.fWishlistId, 
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
                 FROM 
                     Wishlist C 
                 INNER JOIN 
                     item11 i ON i.fItemcode = C.fProductCode
                 INNER JOIN 
                     Division D ON i.fPurity = d.fName
                 WHERE 
                     C.fCusCode =@fCusCode  order by c.fWishlistId desc";

                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@fCusCode", fCusCode);

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
                                decimal taxAmount = result.TaxAmount;



                                WishlistItem item = new WishlistItem
                                {
                                    CartId = reader["fWishlistId"]?.ToString() ?? "",
                                    ItemCode = reader["fItemcode"]?.ToString() ?? "",
                                    fparent = reader["fparent"]?.ToString() ?? "",
                                    ItemName = reader["fItemName"]?.ToString() ?? "",
                                    Image = reader["fimage"]?.ToString() ?? "",
                                    TotalPrice = totalAmount,
                                };

                                wishlistItems.Add(item);
                            }
                        }
                    }
                }

                if (wishlistItems.Count == 0)
                {
                    return Ok(new { wishlistItems = new List<WishlistItem>() });
                }


                return Ok(new
                {
                    wishlistItems
                });
            }
            catch (SqlException sqlEx)
            {
                return StatusCode(500, new { message = "Database error occurred.", details = sqlEx.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An unexpected error occurred.", details = ex.Message });
            }
        }







        private decimal SafeGetDecimal(SqlDataReader reader, string columnName)
        {
            object value = reader[columnName];
            if (value == DBNull.Value || string.IsNullOrWhiteSpace(value.ToString()))
                return 0;

            return Convert.ToDecimal(value);
        }




        [HttpDelete("WishlistDeleteItem/{itemCode}")]
        public IActionResult RemoveCartItem(string itemCode, [FromQuery] string fCusCode)
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(DBHelper.GetConnection()))
                {
                    connection.Open();

                    string query = @"
                DELETE FROM Wishlist 
                WHERE fProductCode = @itemCode AND fCusCode = @fCusCode";

                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@itemCode", itemCode);
                        command.Parameters.AddWithValue("@fCusCode", fCusCode);

                        int rowsAffected = command.ExecuteNonQuery();

                        if (rowsAffected > 0)
                        {
                            return Ok("Item removed successfully.");
                        }
                        else
                        {
                            return NotFound();
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







public class Wishlist
{
    public string ProductCode { get; set; }
    public string CusCode { get; set; }
}

public class WishlistItem
{

    public string CartId { get; set; }
    public string ItemCode { get; set; }
    public string fparent { get; set; }
    public string ItemName { get; set; }
    public string Image { get; set; }
    public decimal TodayRate { get; set; }
    public decimal TotalPrice { get; set; }

}





























//[HttpGet]
//[Route("WishlistViewItem")]
//public async Task<IActionResult> GetWishlistItems(string fCusCode)
//{
//    List<WishlistItem> wishlistItems = new List<WishlistItem>();


//    try
//    {
//        using (SqlConnection connection = new SqlConnection(DBHelper.GetConnection()))
//        {
//            await connection.OpenAsync();

//            string query = @"
//                        SELECT 
//                     C.fWishlistId, 
//	                 i.fparent,
//                     i.fItemcode, 
//                     i.fItemName, 
//                     i.fimage, 
//                     i.Weight, 
//                     i.NetWt, 
//                     i.fVA, 
//                     i.fVAGMS, 
//                     i.fMc, 
//                     i.fOthers, 
//                     i.fTax, 
//                     i.fStoneCharges, 
//                     i.fPieceRate, 
//                     i.fRate,
//                     d.fRate AS GoldRate
//                 FROM 
//                     Wishlist C 
//                 INNER JOIN 
//                     item i ON i.fItemcode = C.fProductCode
//                 INNER JOIN 
//                     Division D ON i.fPurity = d.fName
//                 WHERE 
//                     C.fCusCode =@fCusCode";

//            using (SqlCommand command = new SqlCommand(query, connection))
//            {
//                command.Parameters.AddWithValue("@fCusCode", fCusCode);

//                using (SqlDataReader reader = await command.ExecuteReaderAsync())
//                {
//                    while (await reader.ReadAsync())
//                    {
//                        string pieceRate = reader["fPieceRate"]?.ToString();
//                        decimal weight = SafeGetDecimal(reader, "Weight");
//                        decimal NetWt = SafeGetDecimal(reader, "NetWt");
//                        decimal vaPercent = SafeGetDecimal(reader, "fVA");
//                        decimal vaGrams = SafeGetDecimal(reader, "fVAGMS");
//                        decimal mc = SafeGetDecimal(reader, "fMc");
//                        decimal others = SafeGetDecimal(reader, "fOthers");
//                        decimal stoneCharges = SafeGetDecimal(reader, "fStoneCharges");
//                        decimal taxPercent = SafeGetDecimal(reader, "fTax");
//                        decimal goldRate = SafeGetDecimal(reader, "GoldRate");
//                        decimal fRate = SafeGetDecimal(reader, "fRate");

//                        decimal totalItemPrice = 0;
//                        decimal todayRate = 0;

//                        if (pieceRate == "Y")
//                        {
//                            totalItemPrice = fRate + mc + others + stoneCharges;
//                        }
//                        else
//                        {
//                            decimal totalWastage = (vaGrams > 0) ? vaGrams : (NetWt * vaPercent / 100);
//                            decimal totalWeightWithWastage = NetWt + totalWastage;

//                            todayRate = totalWeightWithWastage * goldRate;

//                            totalItemPrice = todayRate + mc + others + stoneCharges;
//                        }

//                        decimal taxAmount = (taxPercent > 0) ? (totalItemPrice * taxPercent / 100) : 0;
//                        totalItemPrice += taxAmount;


//                        WishlistItem item = new WishlistItem
//                        {
//                            CartId = reader["fWishlistId"]?.ToString() ?? "",
//                            ItemCode = reader["fItemcode"]?.ToString() ?? "",
//                            fparent = reader["fparent"]?.ToString() ?? "",
//                            ItemName = reader["fItemName"]?.ToString() ?? "",
//                            Image = reader["fimage"]?.ToString() ?? "",
//                            TodayRate = todayRate,
//                            TotalPrice = totalItemPrice,
//                        };

//                        wishlistItems.Add(item);
//                    }
//                }
//            }
//        }

//        if (wishlistItems.Count == 0)
//        {
//            return NotFound(new { message = "No items found in the wishlist." });
//        }

//        return Ok(new
//        {
//            wishlistItems
//        });
//    }
//    catch (SqlException sqlEx)
//    {
//        return StatusCode(500, new { message = "Database error occurred.", details = sqlEx.Message });
//    }
//    catch (Exception ex)
//    {
//        return StatusCode(500, new { message = "An unexpected error occurred.", details = ex.Message });
//    }
//}