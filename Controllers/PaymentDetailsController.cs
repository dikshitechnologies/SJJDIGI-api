using System.Data;
using JEWELLBISREACT.DBConnection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace CHITSCHEME.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PaymentDetailsController : ControllerBase
    {
        [HttpPost("paymentDetails")]
        public IActionResult AddPayment([FromBody] PaymentDetailsModel payment)
        {
            

            using (SqlConnection con = new SqlConnection(DBHelper.GetConnection()))
            {
                con.Open();

                // ✅ Step 1: Check if same record already exists
                string checkQuery = @"SELECT COUNT(*) FROM PaymentDetails 
                              WHERE FchitCode = @FchitCode 
                                AND FcusCode = @FcusCode 
                                AND FWeight = @FWeight 
                                AND FAmount = @FAmount";

                using (SqlCommand checkCmd = new SqlCommand(checkQuery, con))
                {
                    checkCmd.Parameters.AddWithValue("@FchitCode", payment.FchitCode);
                    checkCmd.Parameters.AddWithValue("@FcusCode", payment.FcusCode);
                    checkCmd.Parameters.AddWithValue("@FWeight", payment.FWeight);
                    checkCmd.Parameters.AddWithValue("@FAmount", payment.FAmount);

                    int count = Convert.ToInt32(checkCmd.ExecuteScalar());

                    if (count > 0)
                    {
                        return Conflict(new
                        {
                            Message = "Duplicate payment not allowed for same Chit, Customer, Weight, and Amount."
                        });
                    }
                }

                // ✅ Step 2: Insert record (only if no duplicate found)
                string insertQuery = @"INSERT INTO PaymentDetails (FDate, FchitCode, FcusCode, FWeight, FAmount)
                               VALUES (@FDate, @FchitCode, @FcusCode, @FWeight, @FAmount)";

                using (SqlCommand cmd = new SqlCommand(insertQuery, con))
                {
                    cmd.Parameters.AddWithValue("@FDate", DateTime.Now);
                    cmd.Parameters.AddWithValue("@FchitCode", payment.FchitCode);
                    cmd.Parameters.AddWithValue("@FcusCode", payment.FcusCode);
                    cmd.Parameters.AddWithValue("@FWeight", payment.FWeight);
                    cmd.Parameters.AddWithValue("@FAmount", payment.FAmount);
                    cmd.ExecuteNonQuery();
                }

                con.Close();
            }

            return Ok(new
            {
                Message = "Payment added successfully",
            });
        }




        //[HttpGet("GetPaymentDetails")]
        //public IActionResult GetPaymentDetails(int pageNumber = 1, int pageSize = 10)
        //{
        //    try
        //    {
        //        if (pageNumber < 1) pageNumber = 1;
        //        if (pageSize < 1) pageSize = 10;

        //        int offset = (pageNumber - 1) * pageSize;
        //        string query = @"
        //    SELECT 
        //        P.Id,
        //        P.FDate,
        //        P.FchitCode,
        //        ChitParty.fAcname AS ChitName,
        //        P.FcusCode,
        //        CusParty.fAcname AS CustomerName,
        //        P.FWeight,
        //        P.FAmount
        //    FROM PaymentDetails P
        //    LEFT JOIN Party AS ChitParty ON ChitParty.fCode = P.FchitCode
        //    LEFT JOIN Party AS CusParty ON CusParty.fCode = P.FcusCode
        //    ORDER BY P.Id DESC
        //    OFFSET @Offset ROWS
        //    FETCH NEXT @PageSize ROWS ONLY;

        //    SELECT COUNT(*) AS TotalRecords FROM PaymentDetails;
        //"
        //        ;

        //        DataSet ds = new DataSet();
        //        int totalRecords = 0;

        //        using (SqlConnection con = new SqlConnection(DBHelper.GetConnection()))
        //        {
        //            using (SqlCommand cmd = new SqlCommand(query, con))
        //            {
        //                cmd.Parameters.AddWithValue("@Offset", offset);
        //                cmd.Parameters.AddWithValue("@PageSize", pageSize);

        //                using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
        //                {
        //                    adapter.Fill(ds);
        //                }
        //            }
        //        }

        //        DataTable table = ds.Tables[0];
        //        if (ds.Tables.Count > 1 && ds.Tables[1].Rows.Count > 0)
        //            totalRecords = Convert.ToInt32(ds.Tables[1].Rows[0]["TotalRecords"]);

        //        // ✅ Convert DataTable → List<Dictionary<string, object>>
        //        var dataList = new List<Dictionary<string, object>>();
        //        foreach (DataRow row in table.Rows)
        //        {
        //            var dict = new Dictionary<string, object>();
        //            foreach (DataColumn col in table.Columns)
        //            {
        //                dict[col.ColumnName] = row[col];
        //            }
        //            dataList.Add(dict);
        //        }

        //        return Ok(new
        //        {
        //            PageNumber = pageNumber,
        //            PageSize = pageSize,
        //            TotalRecords = totalRecords,
        //            TotalPages = (int)Math.Ceiling((double)totalRecords / pageSize),
        //            Data = dataList
        //        });
        //    }
        //    catch (Exception ex)
        //    {
        //        return BadRequest(new { Message = "Error retrieving data", Error = ex.Message });
        //    }
        //}


        [HttpGet("GetPaymentDetails")]
        public IActionResult GetPaymentDetails(
    int pageNumber = 1,
    int pageSize = 10,
    DateTime? fromDate = null,
    DateTime? toDate = null,
    string chitName = null,
    string customerName = null)
        {
            try
            {
                if (pageNumber < 1) pageNumber = 1;
                if (pageSize < 1) pageSize = 10;

                int offset = (pageNumber - 1) * pageSize;

                // ✅ Base query
                string query = @"
        SELECT 
            P.Id,
            P.FDate,
            P.FchitCode,
            ChitParty.fAcname AS ChitName,
            P.FcusCode,
            CusParty.fAcname AS CustomerName,
            P.FWeight,
            P.FAmount
        FROM PaymentDetails P
        LEFT JOIN Party AS ChitParty ON ChitParty.fCode = P.FchitCode
        LEFT JOIN Party AS CusParty ON CusParty.fCode = P.FcusCode
        WHERE 1=1
        ";

                // ✅ Add filters dynamically
                if (fromDate.HasValue)
                    query += " AND CAST(P.FDate AS DATE) >= @FromDate";
                if (toDate.HasValue)
                    query += " AND CAST(P.FDate AS DATE) <= @ToDate";
                if (!string.IsNullOrEmpty(chitName))
                    query += " AND ChitParty.fAcname LIKE '%' + @ChitName + '%'";
                if (!string.IsNullOrEmpty(customerName))
                    query += " AND CusParty.fAcname LIKE '%' + @CustomerName + '%'";

                // ✅ Order + Pagination
                query += @"
        ORDER BY P.Id DESC
        OFFSET @Offset ROWS
        FETCH NEXT @PageSize ROWS ONLY;

        -- Count query
        SELECT COUNT(*) AS TotalRecords 
        FROM PaymentDetails P
        LEFT JOIN Party AS ChitParty ON ChitParty.fCode = P.FchitCode
        LEFT JOIN Party AS CusParty ON CusParty.fCode = P.FcusCode
        WHERE 1=1
        ";

                // ✅ Duplicate same filters for count
                if (fromDate.HasValue)
                    query += " AND CAST(P.FDate AS DATE) >= @FromDate";
                if (toDate.HasValue)
                    query += " AND CAST(P.FDate AS DATE) <= @ToDate";
                if (!string.IsNullOrEmpty(chitName))
                    query += " AND ChitParty.fAcname LIKE '%' + @ChitName + '%'";
                if (!string.IsNullOrEmpty(customerName))
                    query += " AND CusParty.fAcname LIKE '%' + @CustomerName + '%'";

                DataSet ds = new DataSet();
                int totalRecords = 0;

                using (SqlConnection con = new SqlConnection(DBHelper.GetConnection()))
                {
                    using (SqlCommand cmd = new SqlCommand(query, con))
                    {
                        cmd.Parameters.AddWithValue("@Offset", offset);
                        cmd.Parameters.AddWithValue("@PageSize", pageSize);

                        if (fromDate.HasValue)
                            cmd.Parameters.AddWithValue("@FromDate", fromDate.Value);
                        if (toDate.HasValue)
                            cmd.Parameters.AddWithValue("@ToDate", toDate.Value);
                        if (!string.IsNullOrEmpty(chitName))
                            cmd.Parameters.AddWithValue("@ChitName", chitName);
                        if (!string.IsNullOrEmpty(customerName))
                            cmd.Parameters.AddWithValue("@CustomerName", customerName);

                        using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                        {
                            adapter.Fill(ds);
                        }
                    }
                }

                DataTable table = ds.Tables[0];
                if (ds.Tables.Count > 1 && ds.Tables[1].Rows.Count > 0)
                    totalRecords = Convert.ToInt32(ds.Tables[1].Rows[0]["TotalRecords"]);

                var dataList = new List<Dictionary<string, object>>();
                foreach (DataRow row in table.Rows)
                {
                    var dict = new Dictionary<string, object>();
                    foreach (DataColumn col in table.Columns)
                    {
                        dict[col.ColumnName] = row[col];
                    }
                    dataList.Add(dict);
                }

                return Ok(new
                {
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    TotalRecords = totalRecords,
                    TotalPages = (int)Math.Ceiling((double)totalRecords / pageSize),
                    Data = dataList
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Message = "Error retrieving data", Error = ex.Message });
            }
        }





        //[HttpPut("UpdatePaymentDetails")]
        //public IActionResult UpdatePaymentDetails([FromBody] PaymentDetails payment)
        //{
        //    try
        //    {
        //        if (payment == null || payment.Id <= 0)
        //            return BadRequest(new { Message = "Invalid payment data or ID." });

        //        string query = @"
        //    UPDATE PaymentDetails
        //    SET 
        //        FDate = ISNULL(@FDate, GETDATE()),
        //        FchitCode = @FchitCode,
        //        FcusCode = @FcusCode,
        //        fAmount = @fAmount,
        //        FWeight = @FWeight
        //    WHERE Id = @Id;
        //";

        //        using (SqlConnection con = new SqlConnection(DBHelper.GetConnection()))
        //        {
        //            using (SqlCommand cmd = new SqlCommand(query, con))
        //            {
        //                cmd.Parameters.AddWithValue("@Id", payment.Id);
        //                cmd.Parameters.AddWithValue("@FDate", DateTime.Now);
        //                cmd.Parameters.AddWithValue("@FchitCode", (object?)payment.FchitCode ?? DBNull.Value);
        //                cmd.Parameters.AddWithValue("@FcusCode", (object?)payment.FcusCode ?? DBNull.Value);
        //                cmd.Parameters.AddWithValue("@fAmount", payment.fAmount);
        //                cmd.Parameters.AddWithValue("@FWeight", payment.FWeight);

        //                con.Open();
        //                int rows = cmd.ExecuteNonQuery();

        //                if (rows > 0)
        //                    return Ok(new { Message = "Payment details updated successfully." });
        //                else
        //                    return NotFound(new { Message = "Payment record not found." });
        //            }
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        return BadRequest(new { Message = "Error updating payment details", Error = ex.Message });
        //    }
        //}

        public class PaymentDetails
        {
            public int Id { get; set; }

            // Nullable — if not provided, backend will use current date
            public DateTime? FDate { get; set; }

            // Chit code (like scheme or plan)
            public string FchitCode { get; set; }

            // Customer code (linked to Party table)
            public string FcusCode { get; set; }

            // Amount of payment
            public decimal fAmount { get; set; }

            // Weight (optional, depending on your business logic)
            public decimal FWeight { get; set; }
        }


        public class PaymentDetailsModel
        {
            //public DateTime FDate { get; set; }
            public string FchitCode { get; set; }
            public string FcusCode { get; set; }
            public decimal FWeight { get; set; }
            public decimal FAmount { get; set; }
        }


    }
}
