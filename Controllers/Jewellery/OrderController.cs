using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using JEWELLBISREACT.DBConnection;
using Microsoft.AspNetCore.Authorization;

namespace CHITSCHEME.Controllers.Jewellery
{
    
    [Route("api/[controller]")]
    [Authorize]
    [ApiController]
    public class OrderController : ControllerBase
    {
        [HttpPost("placeOrder")] 
        public async Task<IActionResult> PlaceOrder([FromBody] OrderModel order)
        {
            if (order == null || order.Items == null || !order.Items.Any())
                return BadRequest("Invalid order data.");

            using (SqlConnection conn = new SqlConnection(DBHelper.GetConnection()))
            {
                await conn.OpenAsync();

                SqlTransaction transaction = conn.BeginTransaction();

                try
                {
                    // Insert into Orders table
                    string insertOrderQuery = @"
                    INSERT INTO Orders 
                    (CustomerCode, DeliveryAddress, City, State, Pincode, PaymentMethod, OrderDate)
                    VALUES (@CustomerCode, @DeliveryAddress, @City, @State, @Pincode, @PaymentMethod, GETDATE());
                    SELECT SCOPE_IDENTITY();";

                    SqlCommand cmdOrder = new SqlCommand(insertOrderQuery, conn, transaction);
                    cmdOrder.Parameters.AddWithValue("@CustomerCode", order.CustomerCode);
                    cmdOrder.Parameters.AddWithValue("@DeliveryAddress", order.DeliveryAddress);
                    cmdOrder.Parameters.AddWithValue("@City", order.City);
                    cmdOrder.Parameters.AddWithValue("@State", order.State);
                    cmdOrder.Parameters.AddWithValue("@Pincode", order.Pincode);
                    cmdOrder.Parameters.AddWithValue("@PaymentMethod", order.PaymentMethod);

                    int orderId = Convert.ToInt32(await cmdOrder.ExecuteScalarAsync());

                    // Insert items into OrderItems table
                    foreach (var item in order.Items)
                    {
                        string insertItemQuery = @"
                        INSERT INTO OrderItems (OrderID, ItemCode, Quantity, Price)
                        VALUES (@OrderID, @ItemCode, @Quantity, @Price);";

                        SqlCommand cmdItem = new SqlCommand(insertItemQuery, conn, transaction);
                        cmdItem.Parameters.AddWithValue("@OrderID", orderId);
                        cmdItem.Parameters.AddWithValue("@ItemCode", item.ItemCode);
                        cmdItem.Parameters.AddWithValue("@Quantity", item.Quantity);
                        cmdItem.Parameters.AddWithValue("@Price", item.Price);
                        await cmdItem.ExecuteNonQueryAsync();



                        string deleteCartQuery = "DELETE FROM cartlist WHERE fCusid = @CustomerCode AND fProductCode = @ItemCode";
                        SqlCommand cmdDeleteCart = new SqlCommand(deleteCartQuery, conn, transaction);
                        cmdDeleteCart.Parameters.AddWithValue("@CustomerCode", order.CustomerCode);
                        cmdDeleteCart.Parameters.AddWithValue("@ItemCode", item.ItemCode);
                        await cmdDeleteCart.ExecuteNonQueryAsync();
                    }

                    transaction.Commit();
                    return Ok(new { Message = "Order placed successfully", OrderId = orderId });
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    return StatusCode(500, new { Message = "Order failed", Error = ex.Message });
                }
            }
        }


        [HttpGet("GetOrderReport/{customerCode}")]
        public async Task<IActionResult> GetOrderReport(string customerCode)
        {
            var orderReport = new List<object>();

            string query = @"
        SELECT 
            o.OrderID,
            o.CustomerCode,
            o.OrderDate,
            o.DeliveryStatus,
            i.ItemCode,
            item.fItemName,
            item.fImage,
            i.Quantity,
            i.Price,
            (i.Quantity * i.Price) AS TotalPrice
        FROM Orders o
        JOIN OrderItems i ON o.OrderID = i.OrderID
        JOIN Item11 item ON item.fItemcode = i.ItemCode
        WHERE o.CustomerCode = @CustomerCode
        ORDER BY o.OrderDate DESC;";

            using (SqlConnection conn = new SqlConnection(DBHelper.GetConnection()))
            using (SqlCommand cmd = new SqlCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@CustomerCode", customerCode);
                await conn.OpenAsync();

                using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        orderReport.Add(new
                        {
                            OrderID = reader["OrderID"],
                            CustomerCode = reader["CustomerCode"],
                            OrderDate = Convert.ToDateTime(reader["OrderDate"]).ToString("yyyy-MM-dd HH:mm:ss"),
                            OrderStatus = reader["DeliveryStatus"].ToString(),
                            ItemCode = reader["ItemCode"],
                            ItemName = reader["fItemName"],
                            Image = reader["fImage"],
                            Quantity = Convert.ToInt32(reader["Quantity"]),
                            Price = Convert.ToDecimal(reader["Price"]),
                            TotalPrice = Convert.ToDecimal(reader["TotalPrice"])
                        });
                    }
                }
            }

            return Ok(new { orders = orderReport });
        }



    }
}



public class OrderModel
{
    public string CustomerCode { get; set; }
    public string DeliveryAddress { get; set; }
    public string City { get; set; }
    public string State { get; set; }
    public string Pincode { get; set; }
    public string PaymentMethod { get; set; }
    public List<OrderItemModel> Items { get; set; }
}

public class OrderItemModel
{
    public string ItemCode { get; set; }
    public int Quantity { get; set; }
    public decimal Price { get; set; }
}
