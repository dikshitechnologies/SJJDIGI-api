using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using QRCoder;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using JEWELLBISREACT.DBConnection;

namespace CHITSCHEME.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PaymentController : ControllerBase
    {

        [HttpPost("verify")]
        public IActionResult Verify([FromBody] PaymentDto dto)
        {
            if (dto == null || dto.Amount <= 0 || string.IsNullOrEmpty(dto.TransactionRef))
                return BadRequest("Invalid input");

            using (var conn = new SqlConnection(DBHelper.GetConnection()))
            {
                conn.Open();
                var cmd = new SqlCommand(@"
                INSERT INTO Payments (TransactionRef, Amount, UpiId, RawMessage, Status)
                VALUES (@ref, @amt, @upi, @msg, 'Success')", conn);

                cmd.Parameters.AddWithValue("@ref", dto.TransactionRef);
                cmd.Parameters.AddWithValue("@amt", dto.Amount);
                cmd.Parameters.AddWithValue("@upi", dto.UpiId ?? "");
                cmd.Parameters.AddWithValue("@msg", dto.Message ?? "");
                cmd.ExecuteNonQuery();
            }

            return Ok(new { message = "Saved" });
        }


        [HttpPost("SavePaymentResponse")]
        public IActionResult SavePaymentResponse([FromBody] PaymentResponseDto request)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(DBHelper.GetConnection()))
                {
                    conn.Open();
                    using (SqlCommand cmd = new SqlCommand(@"
                INSERT INTO PaymentResponses (
                    EasePayID, TxnID, Status, Result, Amount, PaymentMethod, CardType,
                    CardNumber, BankName, IssuingBank, Mode, AuthCode, BankRefNum,
                    Phone, Email, FirstName, AddedOn, PaymentSource, ProductInfo, ErrorMessage, RawResponse
                )
                VALUES (
                    @EasePayID, @TxnID, @Status, @Result, @Amount, @PaymentMethod, @CardType,
                    @CardNumber, @BankName, @IssuingBank, @Mode, @AuthCode, @BankRefNum,
                    @Phone, @Email, @FirstName, @AddedOn, @PaymentSource, @ProductInfo, @ErrorMessage, @RawResponse
                )", conn))
                    {
                        cmd.Parameters.AddWithValue("@EasePayID", request.EasePayID ?? "");
                        cmd.Parameters.AddWithValue("@TxnID", request.TxnID ?? "");
                        cmd.Parameters.AddWithValue("@Status", request.Status ?? "");
                        cmd.Parameters.AddWithValue("@Result", request.Result ?? "");
                        cmd.Parameters.AddWithValue("@Amount", request.Amount);
                        cmd.Parameters.AddWithValue("@PaymentMethod", request.PaymentMethod ?? "");
                        cmd.Parameters.AddWithValue("@CardType", request.CardType ?? "");
                        cmd.Parameters.AddWithValue("@CardNumber", request.CardNumber ?? "");
                        cmd.Parameters.AddWithValue("@BankName", request.BankName ?? "");
                        cmd.Parameters.AddWithValue("@IssuingBank", request.IssuingBank ?? "");
                        cmd.Parameters.AddWithValue("@Mode", request.Mode ?? "");
                        cmd.Parameters.AddWithValue("@AuthCode", request.AuthCode ?? "");
                        cmd.Parameters.AddWithValue("@BankRefNum", request.BankRefNum ?? "");
                        cmd.Parameters.AddWithValue("@Phone", request.Phone ?? "");
                        cmd.Parameters.AddWithValue("@Email", request.Email ?? "");
                        cmd.Parameters.AddWithValue("@FirstName", request.FirstName ?? "");
                        cmd.Parameters.AddWithValue("@AddedOn", (object?)request.AddedOn ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@PaymentSource", request.PaymentSource ?? "");
                        cmd.Parameters.AddWithValue("@ProductInfo", request.ProductInfo ?? "");
                        cmd.Parameters.AddWithValue("@ErrorMessage", request.ErrorMessage ?? "");
                        cmd.Parameters.AddWithValue("@RawResponse", request.RawResponse ?? "");

                        cmd.ExecuteNonQuery();
                    }
                }

                return Ok(new { status = true, message = "Payment response saved successfully." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { status = false, message = "Error saving payment response.", error = ex.Message });
            }
        }

    }
}






public class PaymentResponseDto
{
    public string EasePayID { get; set; }
    public string TxnID { get; set; }
    public string Status { get; set; }
    public string Result { get; set; }
    public decimal Amount { get; set; }
    public string PaymentMethod { get; set; }
    public string CardType { get; set; }
    public string CardNumber { get; set; }
    public string BankName { get; set; }
    public string IssuingBank { get; set; }
    public string Mode { get; set; }
    public string AuthCode { get; set; }
    public string BankRefNum { get; set; }
    public string Phone { get; set; }
    public string Email { get; set; }
    public string FirstName { get; set; }
    public DateTime? AddedOn { get; set; }
    public string PaymentSource { get; set; }
    public string ProductInfo { get; set; }
    public string ErrorMessage { get; set; }
    public string RawResponse { get; set; }
}

public class PaymentDto
    {
        public string TransactionRef { get; set; }
        public decimal Amount { get; set; }
        public string UpiId { get; set; }
        public string Message { get; set; }
    }

      
    

  