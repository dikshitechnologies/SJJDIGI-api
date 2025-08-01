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
    public class CartViewController : ControllerBase
    {
        [HttpPost("AddToCart")]
        public async Task<IActionResult> AddToCart([FromBody] Cart cart)
        {
            if (cart == null || string.IsNullOrEmpty(cart.ProductCode))
            {
                return BadRequest(new { error = "Invalid cart data. Product code is required." });
            }

            try
            {
                using (SqlConnection connection = new SqlConnection(DBHelper.GetConnection()))
                {
                    await connection.OpenAsync();

                    string checkProductQuery = "SELECT COUNT(*) FROM cartlist WHERE fCusid = @cusid AND fProductCode = @productCode";
                    using (SqlCommand checkCommand = new SqlCommand(checkProductQuery, connection))
                    {
                        checkCommand.Parameters.AddWithValue("@cusid", cart.CusCode);
                        checkCommand.Parameters.AddWithValue("@productCode", cart.ProductCode);

                        int existingCount = (int)await checkCommand.ExecuteScalarAsync();
                        if (existingCount > 0)
                        {
                            return BadRequest(new { message = "You have already added this product to your cart." });
                        }
                    }

                    string maxCartIdQuery = "SELECT MAX(cartid) FROM cartlist";
                    using (SqlCommand maxIdCommand = new SqlCommand(maxCartIdQuery, connection))
                    {
                        object result = await maxIdCommand.ExecuteScalarAsync();

                        string newCartId;
                        if (result == DBNull.Value || result == null)
                        {
                            newCartId = "00001";
                        }
                        else
                        {
                            string lastCartId = result.ToString();
                            if (int.TryParse(lastCartId, out int lastId))
                            {
                                int nextId = lastId + 1;
                                newCartId = nextId <= 99999 ? nextId.ToString("D5") : nextId.ToString();
                            }
                            else
                            {
                                return StatusCode(500, new { message = "Invalid cart ID format in database." });
                            }
                        }

                        string insertQuery = "INSERT INTO cartlist (cartid, fCusid, fProductCode, Cdate) " +
                                             "VALUES (@cartid, @cusid, @productCode, @date)";

                        using (SqlCommand insertCommand = new SqlCommand(insertQuery, connection))
                        {
                            insertCommand.Parameters.AddWithValue("@cartid", newCartId);
                            insertCommand.Parameters.AddWithValue("@cusid", cart.CusCode);
                            insertCommand.Parameters.AddWithValue("@productCode", cart.ProductCode);
                            insertCommand.Parameters.AddWithValue("@date", DateTime.Now);

                            int insertResult = await insertCommand.ExecuteNonQueryAsync();

                            if (insertResult > 0)
                            {
                                return Ok(new { message = "Item added to cart successfully.", cartid = newCartId });
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
                return StatusCode(500, new { message = "Database error occurred.", details = sqlEx.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An unexpected error occurred.", details = ex.Message });
            }
        }


        [HttpGet("CartItemCount/{fCusid}")]
        public async Task<IActionResult> GetCartItemCount([FromRoute] string fCusid)
        {
            try
            {
                int count = 0;

                using (SqlConnection conn = new SqlConnection(DBHelper.GetConnection()))
                {
                    await conn.OpenAsync();

                    string query = "SELECT COUNT(*) FROM cartlist WHERE fCusid = @fCusid";

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
                return StatusCode(500, new { error = "Database error occurred.", details = sqlEx.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Unexpected error occurred.", details = ex.Message });
            }
        }


        [HttpGet]
        [Route("cartViewItem")]
        public async Task<IActionResult> GetCartItems(string fCusid)
        {
            List<CartItem> cartItems = new List<CartItem>();
            decimal AlltotalAmount = 0;

            try
            {
                using (SqlConnection connection = new SqlConnection(DBHelper.GetConnection()))
                {
                    await connection.OpenAsync();

                    string query = @"
                            SELECT 
                        C.cartid, 
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
                        CartList C 
                    INNER JOIN 
                        item11 i ON i.fItemcode = C.fProductCode
                    INNER JOIN 
                        Division D ON i.fPurity = d.fName
                    WHERE 
                        C.fCusid = @fCusid  order by c.cartid  desc";

                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@fCusid", fCusid);

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
                                AlltotalAmount += totalAmount;

                                CartItem item = new CartItem
                                {
                                    CartId = reader["cartid"]?.ToString() ?? "",
                                    ItemCode = reader["fItemcode"]?.ToString() ?? "",
                                    fparent = reader["fparent"]?.ToString() ?? "",
                                    ItemName = reader["fItemName"]?.ToString() ?? "",
                                    Image = reader["fimage"]?.ToString() ?? "",
                                    TotalPrice = totalAmount,
                                };

                                cartItems.Add(item);
                            }
                        }
                    }
                }

                if (cartItems.Count == 0)
                {
                    return NotFound(new { message = "No items found in the cart." });
                }

                return Ok(new
                {
                    AlltotalAmount,
                    cartItems
                });
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


        private decimal SafeGetDecimal(SqlDataReader reader, string columnName)
        {
            object value = reader[columnName];
            if (value == DBNull.Value || string.IsNullOrWhiteSpace(value.ToString()))
                return 0;

            return Convert.ToDecimal(value);
        }



        [HttpDelete("cartDeleteItem/{itemCode}")]
        public IActionResult RemoveCartItem(string itemCode, [FromQuery] string fCusid)
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(DBHelper.GetConnection()))
                {
                    connection.Open();

                    string query = @"
                DELETE FROM CartList 
                WHERE fProductCode = @itemCode AND fCusid = @fCusid";

                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@itemCode", itemCode);
                        command.Parameters.AddWithValue("@fCusid", fCusid);

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



public class Cart
{
    public string ProductCode { get; set; }
    public string CusCode { get; set; }
}

public class CartItem
{
    public string CartId { get; set; }
    public string ItemCode { get; set; }
    public string ItemName { get; set; }
    public string fparent { get; set; }
    public string Image { get; set; }
    public decimal TodayRate { get; set; }
    public decimal TotalPrice { get; set; }

}