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
    public class OrderReportsController : ControllerBase
    {
        [HttpGet("PendingOrdersReport")]
        public async Task<IActionResult> GetPendingOrdersReport()
        {
            var result = new List<PendingOrderDto>();

            try
            {
               

                using (SqlConnection con = new SqlConnection(DBHelper.GetConnection()))
                {
                    await con.OpenAsync();

                    var query = @"
                     SELECT 
                        o.OrderID,
                        o.CustomerCode,
                        RU.fAcname,
                        RU.fphone,
                        o.PaymentMethod,
                        o.OrderDate,
                        o.DeliveryStatus
                    FROM Orders o
                    JOIN party RU ON RU.fcode = o.CustomerCode
                    WHERE o.DeliveryStatus = 'Pending'";

                    using (SqlCommand cmd = new SqlCommand(query, con))
                    {
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                result.Add(new PendingOrderDto
                                {
                                    OrderID = reader.GetInt32(0),
                                    CustomerCode = reader.GetString(1),
                                    UserName = reader.GetString(2),
                                    PhoneNumber = reader.GetString(3),
                                    PaymentMethod = reader.GetString(4),
                                    OrderDate = reader.GetDateTime(5),
                                    DeliveryStatus = reader.GetString(6)
                                });
                            }
                        }
                    }
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error retrieving data: {ex.Message}");
            }
        }
        //[HttpGet("PendingOrdersReport")]
        //public async Task<IActionResult> GetPendingOrdersReport()
        //{
        //    var result = new List<PendingOrderDto>();

        //    try
        //    {


        //        using (SqlConnection con = new SqlConnection(DBHelper.GetConnection()))
        //        {
        //            await con.OpenAsync();

        //            var query = @"
        //            SELECT 
        //                o.OrderID,
        //                o.CustomerCode,
        //                RU.UserName,
        //                RU.PhoneNumber,
        //                o.PaymentMethod,
        //                o.OrderDate,
        //                o.DeliveryStatus
        //            FROM Orders o
        //            JOIN RegisterUsers RU ON RU.UserID = o.CustomerCode
        //            WHERE o.DeliveryStatus = 'Pending'";

        //            using (SqlCommand cmd = new SqlCommand(query, con))
        //            {
        //                using (var reader = await cmd.ExecuteReaderAsync())
        //                {
        //                    while (await reader.ReadAsync())
        //                    {
        //                        result.Add(new PendingOrderDto
        //                        {
        //                            OrderID = reader.GetInt32(0),
        //                            CustomerCode = reader.GetString(1),
        //                            UserName = reader.GetString(2),
        //                            PhoneNumber = reader.GetString(3),
        //                            PaymentMethod = reader.GetString(4),
        //                            OrderDate = reader.GetDateTime(5),
        //                            DeliveryStatus = reader.GetString(6)
        //                        });
        //                    }
        //                }
        //            }
        //        }

        //        return Ok(result);
        //    }
        //    catch (Exception ex)
        //    {
        //        return StatusCode(500, $"Error retrieving data: {ex.Message}");
        //    }
        //}

        [HttpGet("DeliveredSummaryDetailed")]
        public async Task<IActionResult> GetDeliveredOrdersDetailed(string startDate, string endDate)
        {
            var result = new List<DeliveredOrderFullDto>();

            try
            {
                if (!DateTime.TryParse(startDate, out DateTime start))
                    return BadRequest("Invalid startDate format. Use yyyy-MM-dd.");

                if (!DateTime.TryParse(endDate, out DateTime end))
                    return BadRequest("Invalid endDate format. Use yyyy-MM-dd.");

                using (SqlConnection con = new SqlConnection(DBHelper.GetConnection()))
                {
                    await con.OpenAsync();

                    // Query to get full order, customer, and item details
                    var query = @"
               SELECT 
                        o.OrderID,
                        o.CustomerCode,
                        ru.fAcname,
                        o.OrderDate,
                        CAST(o.DeliveryAddress AS NVARCHAR(MAX)) AS DeliveryAddress,
                        CAST(o.City AS NVARCHAR(100)) AS City,
                        CAST(o.State AS NVARCHAR(100)) AS State,
                        o.Pincode,
                        o.PaymentMethod,
                        o.DeliveryStatus,
                        i.fItemname,
                        oi.ItemCode,
                        oi.Quantity,
                        oi.Price
                    FROM Orders o
                    JOIN party ru ON ru.fcode = o.CustomerCode
                    JOIN OrderItems oi ON oi.OrderID = o.OrderID
                    LEFT JOIN item11 i ON i.fItemcode = oi.ItemCode
                    WHERE o.DeliveryStatus = 'Delivered'
                  AND o.OrderDate BETWEEN @startDate AND @endDate
                ORDER BY o.OrderID, o.OrderDate DESC";

                    using (SqlCommand cmd = new SqlCommand(query, con))
                    {
                        cmd.Parameters.AddWithValue("@startDate", start);
                        cmd.Parameters.AddWithValue("@endDate", end);

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            int currentOrderId = -1;
                            DeliveredOrderFullDto currentOrder = null;

                            while (await reader.ReadAsync())
                            {
                                int orderId = reader.GetInt32(0);

                                // New order
                                if (orderId != currentOrderId)
                                {
                                    currentOrderId = orderId;

                                    currentOrder = new DeliveredOrderFullDto
                                    {
                                        CustomerDetails = new DeliveredCustomerSummaryDto
                                        {
                                            OrderID = orderId,
                                            CustomerCode = reader.GetString(1),
                                            UserName = reader.GetString(2),
                                            OrderDate = reader.GetDateTime(3),
                                            DeliveryAddress = reader.GetString(4),
                                            City = reader.GetString(5),
                                            State = reader.GetString(6),
                                            Pincode = reader.GetString(7),
                                            PaymentMethod = reader.GetString(8),
                                            DeliveryStatus = reader.GetString(9),
                                            TotalItems = 0,
                                            TotalAmount = 0
                                        },
                                        Items = new List<DeliveredItemDto>()
                                    };

                                    result.Add(currentOrder);
                                }

                                // Add item to the order
                                var qty = reader.GetInt32(12);
                                var price = reader.GetDecimal(13);

                                currentOrder.Items.Add(new DeliveredItemDto
                                {
                                    ItemName = reader.IsDBNull(10) ? null : reader.GetString(10),
                                    ItemCode = reader.GetString(11),
                                    Quantity = qty,
                                    Price = price,
                                    DeliveryStatus = currentOrder.CustomerDetails.DeliveryStatus,
                                    OrderDate = currentOrder.CustomerDetails.OrderDate
                                });

                                currentOrder.CustomerDetails.TotalItems += qty;
                                currentOrder.CustomerDetails.TotalAmount += qty * price;
                            }
                        }
                    }
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal Server Error: {ex.Message}");
            }
        }


        //[HttpGet("DeliveredSummaryDetailed")]
        //public async Task<IActionResult> GetDeliveredOrdersDetailed(string startDate, string endDate)
        //{
        //    var result = new List<DeliveredOrderFullDto>();

        //    try
        //    {
        //        if (!DateTime.TryParse(startDate, out DateTime start))
        //            return BadRequest("Invalid startDate format. Use yyyy-MM-dd.");

        //        if (!DateTime.TryParse(endDate, out DateTime end))
        //            return BadRequest("Invalid endDate format. Use yyyy-MM-dd.");

        //        using (SqlConnection con = new SqlConnection(DBHelper.GetConnection()))
        //        {
        //            await con.OpenAsync();

        //            // Query to get full order, customer, and item details
        //            var query = @"
        //        SELECT 
        //            o.OrderID,
        //            o.CustomerCode,
        //            ru.UserName,
        //            o.OrderDate,
        //            CAST(o.DeliveryAddress AS NVARCHAR(MAX)) AS DeliveryAddress,
        //            CAST(o.City AS NVARCHAR(100)) AS City,
        //            CAST(o.State AS NVARCHAR(100)) AS State,
        //            o.Pincode,
        //            o.PaymentMethod,
        //            o.DeliveryStatus,
        //            i.fItemname,
        //            oi.ItemCode,
        //            oi.Quantity,
        //            oi.Price
        //        FROM Orders o
        //        JOIN RegisterUsers ru ON ru.UserID = o.CustomerCode
        //        JOIN OrderItems oi ON oi.OrderID = o.OrderID
        //        LEFT JOIN item11 i ON i.fItemcode = oi.ItemCode
        //        WHERE o.DeliveryStatus = 'Delivered'
        //          AND o.OrderDate BETWEEN @startDate AND @endDate
        //        ORDER BY o.OrderID, o.OrderDate DESC";

        //            using (SqlCommand cmd = new SqlCommand(query, con))
        //            {
        //                cmd.Parameters.AddWithValue("@startDate", start);
        //                cmd.Parameters.AddWithValue("@endDate", end);

        //                using (var reader = await cmd.ExecuteReaderAsync())
        //                {
        //                    int currentOrderId = -1;
        //                    DeliveredOrderFullDto currentOrder = null;

        //                    while (await reader.ReadAsync())
        //                    {
        //                        int orderId = reader.GetInt32(0);

        //                        // New order
        //                        if (orderId != currentOrderId)
        //                        {
        //                            currentOrderId = orderId;

        //                            currentOrder = new DeliveredOrderFullDto
        //                            {
        //                                CustomerDetails = new DeliveredCustomerSummaryDto
        //                                {
        //                                    OrderID = orderId,
        //                                    CustomerCode = reader.GetString(1),
        //                                    UserName = reader.GetString(2),
        //                                    OrderDate = reader.GetDateTime(3),
        //                                    DeliveryAddress = reader.GetString(4),
        //                                    City = reader.GetString(5),
        //                                    State = reader.GetString(6),
        //                                    Pincode = reader.GetString(7),
        //                                    PaymentMethod = reader.GetString(8),
        //                                    DeliveryStatus = reader.GetString(9),
        //                                    TotalItems = 0,
        //                                    TotalAmount = 0
        //                                },
        //                                Items = new List<DeliveredItemDto>()
        //                            };

        //                            result.Add(currentOrder);
        //                        }

        //                        // Add item to the order
        //                        var qty = reader.GetInt32(12);
        //                        var price = reader.GetDecimal(13);

        //                        currentOrder.Items.Add(new DeliveredItemDto
        //                        {
        //                            ItemName = reader.IsDBNull(10) ? null : reader.GetString(10),
        //                            ItemCode = reader.GetString(11),
        //                            Quantity = qty,
        //                            Price = price,
        //                            DeliveryStatus = currentOrder.CustomerDetails.DeliveryStatus,
        //                            OrderDate = currentOrder.CustomerDetails.OrderDate
        //                        });

        //                        currentOrder.CustomerDetails.TotalItems += qty;
        //                        currentOrder.CustomerDetails.TotalAmount += qty * price;
        //                    }
        //                }
        //            }
        //        }

        //        return Ok(result);
        //    }
        //    catch (Exception ex)
        //    {
        //        return StatusCode(500, $"Internal Server Error: {ex.Message}");
        //    }
        //}



        [HttpGet("DeliveredSummary")]
        public async Task<IActionResult> GetDeliveredOrdersSummary(string startDate, string endDate)
        {
            var result = new List<DeliveredOrderSummaryDto>();
            

            try
            {
                // Convert strings to DateTime
                if (!DateTime.TryParse(startDate, out DateTime start))
                    return BadRequest("Invalid startDate format. Use yyyy-MM-dd.");

                if (!DateTime.TryParse(endDate, out DateTime end))
                    return BadRequest("Invalid endDate format. Use yyyy-MM-dd.");

                using (SqlConnection con = new SqlConnection(DBHelper.GetConnection()))
                {
                    await con.OpenAsync();

                    var query = @"
                SELECT 
                    o.OrderID,
                    o.CustomerCode,
                    o.OrderDate,
                    CAST(o.DeliveryAddress AS NVARCHAR(MAX)) AS DeliveryAddress,
                    CAST(o.City AS NVARCHAR(100)) AS City,
                    CAST(o.State AS NVARCHAR(100)) AS State,
                    o.Pincode,
                    o.PaymentMethod,
                    o.DeliveryStatus,
                    SUM(oi.Quantity) AS TotalItems,
                    SUM(oi.Price * oi.Quantity) AS TotalAmount
                FROM Orders o
                JOIN OrderItems oi ON o.OrderID = oi.OrderID
                WHERE o.DeliveryStatus = 'Delivered'
                  AND o.OrderDate BETWEEN @startDate AND @endDate
                GROUP BY 
                    o.OrderID, o.CustomerCode, o.OrderDate,
                    CAST(o.DeliveryAddress AS NVARCHAR(MAX)),
                    CAST(o.City AS NVARCHAR(100)),
                    CAST(o.State AS NVARCHAR(100)),
                    o.Pincode, o.PaymentMethod, o.DeliveryStatus
                ORDER BY o.OrderDate DESC";

                    using (SqlCommand cmd = new SqlCommand(query, con))
                    {
                        cmd.Parameters.AddWithValue("@startDate", start);
                        cmd.Parameters.AddWithValue("@endDate", end);

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                result.Add(new DeliveredOrderSummaryDto
                                {
                                    OrderID = reader.GetInt32(0),
                                    CustomerCode = reader.GetString(1),
                                    OrderDate = reader.GetDateTime(2),
                                    DeliveryAddress = reader.GetString(3),
                                    City = reader.GetString(4),
                                    State = reader.GetString(5),
                                    Pincode = reader.GetString(6),
                                    PaymentMethod = reader.GetString(7),
                                    DeliveryStatus = reader.GetString(8),
                                    TotalItems = reader.GetInt32(9),
                                    TotalAmount = reader.GetDecimal(10)
                                });
                            }
                        }
                    }

                    return Ok(result);
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal Server Error: {ex.Message}");
            }
        }

        [HttpGet("OrderItems/{orderId}")]
        public async Task<IActionResult> GetOrderItemsByOrderId([FromRoute] int orderId)
        {
            var result = new List<OrderItemDetailDto>();

            try
            {
                using (SqlConnection con = new SqlConnection(DBHelper.GetConnection()))
                {
                    await con.OpenAsync();

                    // Step 1: Check delivery status
                    string statusQuery = "SELECT DeliveryStatus FROM Orders WHERE OrderID = @orderId";
                    using (SqlCommand statusCmd = new SqlCommand(statusQuery, con))
                    {
                        statusCmd.Parameters.AddWithValue("@orderId", orderId);
                        var statusResult = await statusCmd.ExecuteScalarAsync();

                        if (statusResult == null)
                        {
                            return NotFound(new { message = "Order not found." });
                        }

                        string deliveryStatus = statusResult.ToString();

                        if (deliveryStatus == "Delivered")
                        {
                            return BadRequest(new { message = $"Order is Already delivered yet. Current status: {deliveryStatus}" });
                        }
                    }

                    // Step 2: Fetch order items
                    string itemQuery = @"
                SELECT 
                    oi.OrderItemID,
                    oi.ItemCode,
                    i.fitemname,
                    oi.quantity,
                    oi.price ,
                    I.fimage
                FROM OrderItems oi 
                LEFT JOIN item11 i ON i.fItemcode = oi.ItemCode  
                WHERE oi.OrderID = @orderId";

                    using (SqlCommand cmd = new SqlCommand(itemQuery, con))
                    {
                        cmd.Parameters.AddWithValue("@orderId", orderId);

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                result.Add(new OrderItemDetailDto
                                {
                                    OrderItemID = reader.GetInt32(0),
                                    ItemCode = reader.GetString(1),
                                    FItemName = reader.GetString(2),
                                    Quantity = reader.GetInt32(3),
                                    Price = reader.GetDecimal(4),
                                    IMAGE = reader.GetString(5)
                                });
                            }
                        }
                    }
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal Server Error: {ex.Message}");
            }
        }


        [HttpPut("MarkAsDelivered/{orderId}")]
        public async Task<IActionResult> MarkOrderAsDelivered([FromRoute] string orderId)
        {


            try
            {
                using (SqlConnection con = new SqlConnection(DBHelper.GetConnection()))
                {
                    await con.OpenAsync();

                    var query = @"
                UPDATE Orders 
                SET DeliveryStatus = 'Delivered' 
                WHERE OrderID = @orderId AND DeliveryStatus = 'Pending'";

                    using (SqlCommand cmd = new SqlCommand(query, con))
                    {
                        cmd.Parameters.AddWithValue("@orderId", orderId);

                        int rowsAffected = await cmd.ExecuteNonQueryAsync();

                        if (rowsAffected > 0)
                        {
                            return Ok(new { success = true, message = "Order marked as Delivered." });
                        }
                        else
                        {
                            return NotFound(new { success = false, message = "Order not found or already Delivered." });
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


public class PendingOrderDto
{
    public int OrderID { get; set; }
    public string CustomerCode { get; set; }
    public string UserName { get; set; }
    public string PhoneNumber { get; set; }
    public string PaymentMethod { get; set; }
    public DateTime OrderDate { get; set; }
    public string DeliveryStatus { get; set; }
}



public class DeliveredOrderSummaryDto
{
    public int OrderID { get; set; }
    public string CustomerCode { get; set; }
    public DateTime OrderDate { get; set; }
    public string DeliveryAddress { get; set; }
    public string City { get; set; }
    public string State { get; set; }
    public string Pincode { get; set; }
    public string PaymentMethod { get; set; }
    public string DeliveryStatus { get; set; }
    public int TotalItems { get; set; }
    public decimal TotalAmount { get; set; }
}




public class OrderItemDetailDto
{
    public int OrderItemID { get; set; }
    public string ItemCode { get; set; }
    public string FItemName { get; set; }
    public string IMAGE { get; set; }
    public int Quantity { get; set; }
    public decimal Price { get; set; }
}





public class DeliveredItemDto
{
    public string ItemName { get; set; }
    public string ItemCode { get; set; }
    public int Quantity { get; set; }
    public decimal Price { get; set; }
    public string DeliveryStatus { get; set; }
    public DateTime OrderDate { get; set; }
}

public class DeliveredCustomerSummaryDto
{
    public int OrderID { get; set; }
    public string CustomerCode { get; set; }
    public string UserName { get; set; }
    public DateTime OrderDate { get; set; }
    public string DeliveryAddress { get; set; }
    public string City { get; set; }
    public string State { get; set; }
    public string Pincode { get; set; }
    public string PaymentMethod { get; set; }
    public string DeliveryStatus { get; set; }
    public int TotalItems { get; set; }
    public decimal TotalAmount { get; set; }
}

public class DeliveredOrderFullDto
{
    public DeliveredCustomerSummaryDto CustomerDetails { get; set; }
    public List<DeliveredItemDto> Items { get; set; }
}
