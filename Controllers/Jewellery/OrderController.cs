using JEWELLBISREACT.DBConnection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Data;

namespace CHITSCHEME.Controllers.Jewellery
{
    
    [Route("api/[controller]")]
    [Authorize]
    [ApiController]
    public class OrderController : ControllerBase
    {




        [HttpPost("insert-item-transaction")]
        public async Task<IActionResult> PlaceOrderTrans([FromBody] OrderModel order)
        {
            if (order == null || order.Items == null || !order.Items.Any())
                return BadRequest("Invalid order data.");

            try
            {
                using (SqlConnection con = new SqlConnection(DBHelper.GetConnection()))
                {
                    await con.OpenAsync();
                    using (SqlTransaction tran = con.BeginTransaction())
                    {
                        try
                        {
                            // 1. Get current max FVOUCHER number
                            string maxVoucherQuery = @"
                                    SELECT ISNULL(MAX(CAST(SUBSTRING(FVOUCHER, 3, LEN(FVOUCHER)) AS INT)), 0) 
                                    FROM itemtransactionop
                                    WHERE FVOUCHER LIKE 'OD%'";

                            int maxVoucher = 0;
                            using (SqlCommand cmdMax = new SqlCommand(maxVoucherQuery, con, tran))
                            {
                                maxVoucher = Convert.ToInt32(await cmdMax.ExecuteScalarAsync());
                            }

                            int nextVoucher = maxVoucher+1;

                            foreach (var item in order.Items)
                            {
                                // 2. Fetch data from itempurchaseop for given ItemCode + fid
                                string selectQuery = @"
                           

                                    SELECT 
                                         ip.Itemcode, 
                                         ip.Qty AS fTotQty,
                                         ip.Gms AS fGms,
                                         ip.Mc AS fMcAmount,
                                         ip.StnChrg AS fStnChrg,
                                         ip.Wastage AS fWastage,
                                         ip.fPrefix, 
                                         ip.fBox, 
                                         ip.Gross AS fGross,
                                         ip.fSize, 
                                         ip.fDiv, 
                                         ip.fDescription AS fdesc,
                                         ip.fDesign, 
                                         ip.fSection, 
                                         ip.fID,
                                         d.fRate   -- include division rate
                                    FROM itempurchaseop ip
                                    JOIN Division d ON d.fCode = ip.fDiv
                                    WHERE ip.Itemcode = @ItemCode AND ip.FID = @FID;



                            ";

                                DataTable dt = new DataTable();
                                using (SqlCommand cmdSelect = new SqlCommand(selectQuery, con, tran))
                                {
                                    cmdSelect.Parameters.AddWithValue("@ItemCode", item.ItemCode);
                                    cmdSelect.Parameters.AddWithValue("@FID", item.fid);

                                    using (SqlDataAdapter da = new SqlDataAdapter(cmdSelect))
                                    {
                                        da.Fill(dt);
                                    }
                                }

                                if (dt.Rows.Count == 0)
                                    continue; // skip if no match found
                                // 3. Insert into itemtransactionop
                                foreach (DataRow row in dt.Rows)
                                {

                                    string insertQuery = @"
                                        INSERT INTO itemtransactionop
                                        (FVoucher, FItemcode, FType, fTotQty, fGms, fMcAmount, fStnChrg, fWastage, 
                                         fPrefix, fBox, fGross, fSize, fDiv, fCode,fproductId,fRate)
                                        VALUES
                                        (@FVOUCHER, @FItemcode, @FTYpe, @fTotQty, @FGms, @FMcAmount, @FStnChrg, @FWastage,
                                         @FPrefix, @FBox, @FGross, @FSize, @FDiv, @FCode,@productId,@fRate)";


                                    using (SqlCommand cmdInsert = new SqlCommand(insertQuery, con, tran))
                                    {
                                        string formattedVoucher = "OD" + nextVoucher.ToString("D5"); // e.g., OD00001, OD00002
                                        cmdInsert.Parameters.AddWithValue("@FVOUCHER", formattedVoucher);
                                        cmdInsert.Parameters.AddWithValue("@FItemcode", row["Itemcode"].ToString());
                                        cmdInsert.Parameters.AddWithValue("@FTYpe", "OD");
                                        cmdInsert.Parameters.AddWithValue("@fTotQty", 1);
                                        cmdInsert.Parameters.AddWithValue("@FGms", row["fGms"]);
                                        cmdInsert.Parameters.AddWithValue("@FMcAmount", row["fMcAmount"]);
                                        cmdInsert.Parameters.AddWithValue("@FStnChrg", row["fStnChrg"]);
                                        cmdInsert.Parameters.AddWithValue("@FWastage", row["fWastage"]);
                                        cmdInsert.Parameters.AddWithValue("@FPrefix", row["fPrefix"]);
                                        cmdInsert.Parameters.AddWithValue("@FBox", row["fBox"]);
                                        cmdInsert.Parameters.AddWithValue("@FGross", row["fGross"]);
                                        cmdInsert.Parameters.AddWithValue("@FSize", row["fSize"]);
                                        cmdInsert.Parameters.AddWithValue("@FDiv", row["fDiv"]);
                                        cmdInsert.Parameters.AddWithValue("@FCode", item.ItemCode);
                                        cmdInsert.Parameters.AddWithValue("@productId", item.fid);
                                        cmdInsert.Parameters.AddWithValue("@fRate", row["fRate"]);


                                        await cmdInsert.ExecuteNonQueryAsync();
                                    }

                                }
                            }

                            tran.Commit();
                            return Ok(new { Message = "Order placed successfully." });
                        }
                        catch (Exception ex)
                        {
                            tran.Rollback();
                            return StatusCode(500, $"Error placing order: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Database error: {ex.Message}");
            }
        }





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
    public string fid { get; set; }
    public string Price { get; set; }
}
