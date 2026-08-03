using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Data;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;
using JEWELLBISREACT.DBConnection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace CHITSCHEME_PukhRaj.Controllers.Reports
{
    [Route("api/[controller]")]
    [ApiController]
    public class OverallReportController : ControllerBase
    {
        [HttpGet("GetChitList")]
        public async Task<IActionResult> GetChitList()
        {
            try
            {
                var result = new List<ChitListDto>();

                using var conn = new SqlConnection(DBHelper.GetConnection());
                await conn.OpenAsync();

                string query = @"
                    SELECT fcode, fAcname 
                    FROM party 
                    WHERE fparent LIKE '0000100044%' 
                      AND faclevel > 0 
                      AND fcode <> '00044'
                    ORDER BY fAcname";

                using var cmd = new SqlCommand(query, conn);
                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    result.Add(new ChitListDto
                    {
                        FCode = reader["fcode"].ToString(),
                        FAcname = reader["fAcname"].ToString()
                    });
                }

                return Ok(new { success = true, data = result });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpGet("GetDetails")]
        public async Task<IActionResult> GetDetails(
            [FromQuery] string parentCode = null,
            [FromQuery] string fphone = null,
            [FromQuery] string id = null)
        {
            try
            {
                var result = new List<SchemeDetailDto>();

                using var conn = new SqlConnection(DBHelper.GetConnection());
                await conn.OpenAsync();

                string query = @"
                    WITH RankedSchemes AS
                    (
                        SELECT
                            P.FCODE,
                            P.FACNAME,
                            P.FPHONE,
                            P.FAMOUNT,
                            P.FCOMPCODE,
                            P.FDATE,
                            CASE
                                WHEN PARENT.FCODE = '00103'
                                    THEN DATEADD(MONTH, P.FDUE, P.FDATE)
                                WHEN PARENT.FCODE IN ('03026','03247','03248')
                                    THEN DATEADD(DAY, 330, P.FDATE)
                                ELSE P.FDATE
                            END AS MaturityDate,
                            P.FDUE,
                            P.FID AS SCHEMECODE,
                            CASE
                                WHEN (P.FSHOW = '1' OR (P.FSCHEMETYPE NOT IN ('WT','AT','W') AND P.FDIGITYPE NOT IN ('DS','AT','WT')))
                                THEN 'Active'
                                ELSE 'Inactive'
                            END AS ActiveStatus,
                            ISNULL(L.MaxDue,0) AS PaidDue,
                            ISNULL(T.TotalAmount,0) AS TotalAmount,
                            ISNULL(T.TotalWeight,0) AS TotalWeight,
                            ISNULL(T.TotalBenefitAmt,0) AS TotalBenefitAmt,
                            ISNULL(T.TotalBenefitWt,0) AS TotalBenefitWt,
                            PARENT.FACNAME AS SCHEMENAME
                        FROM PARTY P
                        LEFT JOIN
                        (
                            SELECT FID, MAX(FDUE) AS MaxDue
                            FROM LEDGER
                            WHERE FCRDB='CR' AND FTYPE='CT'
                            GROUP BY FID
                        ) L ON P.FID = L.FID
                        LEFT JOIN
                        (
                            SELECT
                                L.FID,
                                SUM(ISNULL(L.FVRAMOUNT,0)) AS TotalAmount,
                                SUM(CASE WHEN ISNUMERIC(B.FWT)=1 THEN CAST(B.FWT AS DECIMAL(18,3)) ELSE 0 END) AS TotalWeight,
                                SUM(CASE WHEN ISNUMERIC(B.FBAMT)=1 THEN CAST(B.FBAMT AS DECIMAL(18,2)) ELSE 0 END) AS TotalBenefitAmt,
                                SUM(CASE WHEN ISNUMERIC(B.FBWT)=1 THEN CAST(B.FBWT AS DECIMAL(18,3)) ELSE 0 END) AS TotalBenefitWt
                            FROM LEDGER L
                            INNER JOIN BLEDGER B ON B.FVOUCHNO = L.FVRNO
                            WHERE L.FCRDB='CR' AND L.FTYPE='CT'
                            GROUP BY L.FID
                        ) T ON P.FID = T.FID
                        LEFT JOIN PARTY PARENT ON PARENT.FPARENT = LEFT(P.FPARENT, LEN(P.FPARENT)-5)
                        WHERE P.FPARENT LIKE '0000100044%'
                          AND (@ParentCode IS NULL OR PARENT.FCODE = @ParentCode)
                          AND (@FPHONE IS NULL OR P.FPHONE = @FPHONE)
                          AND (@ID IS NULL OR P.FID = @ID)    
                    )
                    SELECT
                        FCODE,
                        FACNAME,
                        FPHONE,
                        FAMOUNT,
                        FCOMPCODE,
                        FDATE,
                        MaturityDate,
                        FDUE,
                        SCHEMECODE,
                        PaidDue,
                        TotalAmount,
                        TotalWeight,
                        TotalBenefitAmt,
                        TotalBenefitWt,
                        SCHEMENAME,
                        ActiveStatus
                    FROM RankedSchemes
                    ORDER BY FACNAME;";

                using var cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@ParentCode", (object)parentCode ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@FPHONE", (object)fphone ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@ID", (object)id ?? DBNull.Value);

                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    result.Add(new SchemeDetailDto
                    {
                        FCode = reader["FCODE"].ToString(),
                        FAcname = reader["FACNAME"].ToString(),
                        FPhone = reader["FPHONE"].ToString(),
                        FAmount = Convert.ToDecimal(reader["FAMOUNT"]),
                        FCompCode = reader["FCOMPCODE"].ToString(),
                        FDate = Convert.ToDateTime(reader["FDATE"]),
                        MaturityDate = Convert.ToDateTime(reader["MaturityDate"]),
                        FDue = Convert.ToInt32(reader["FDUE"]),
                        SchemeCode = reader["SCHEMECODE"].ToString(),
                        PaidDue = Convert.ToInt32(reader["PaidDue"]),
                        TotalAmount = Convert.ToDecimal(reader["TotalAmount"]),
                        TotalWeight = Convert.ToDecimal(reader["TotalWeight"]),
                        TotalBenefitAmt = Convert.ToDecimal(reader["TotalBenefitAmt"]),
                        TotalBenefitWt = Convert.ToDecimal(reader["TotalBenefitWt"]),
                        SchemeName = reader["SCHEMENAME"].ToString(),
                        ActiveStatus = reader["ActiveStatus"].ToString()
                    });
                }

                return Ok(new { success = true, data = result, totalCount = result.Count });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpGet("GetPaymentDetails")]
        public async Task<IActionResult> GetPaymentDetails(
            [FromQuery] string customerCode = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            try
            {
                if (page < 1) page = 1;
                if (pageSize < 1) pageSize = 10;
                if (pageSize > 100) pageSize = 100;

                var result = new List<PaymentDetailDto>();
                int totalCount = 0;

                using var conn = new SqlConnection(DBHelper.GetConnection());
                await conn.OpenAsync();

                // Get total count for pagination
                string countQuery = @"
                    SELECT COUNT(*)
                    FROM BLEDGER B
                    LEFT JOIN PARTY P ON P.FCODE = B.FCUCODE
                    WHERE B.fbilltype='CT'
                      AND (@CustomerCode IS NULL OR @CustomerCode='' OR B.FCUCODE = @CustomerCode)";

                using var countCmd = new SqlCommand(countQuery, conn);
                countCmd.Parameters.AddWithValue("@CustomerCode", (object)customerCode ?? DBNull.Value);
                totalCount = Convert.ToInt32(await countCmd.ExecuteScalarAsync());

                // Get paginated data
                string query = @"
                    SELECT
                        P.FID,
                        B.fcucode,
                        P.FACNAME,
                        B.FWT,
                        B.FBILLAMT,
                        B.FONLINE,
                        B.FVOUCHNO,
                        B.fVouchdt
                    FROM BLEDGER B
                    LEFT JOIN PARTY P ON P.FCODE = B.FCUCODE
                    WHERE B.fbilltype='CT'
                      AND (@CustomerCode IS NULL OR @CustomerCode='' OR B.FCUCODE = @CustomerCode)
                    ORDER BY B.fVouchdt DESC
                    OFFSET @Offset ROWS
                    FETCH NEXT @PageSize ROWS ONLY;";

                using var cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@CustomerCode", (object)customerCode ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Offset", (page - 1) * pageSize);
                cmd.Parameters.AddWithValue("@PageSize", pageSize);

                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    result.Add(new PaymentDetailDto
                    {
                        FID = reader["FID"] != DBNull.Value ? reader["FID"].ToString() : null,
                        FCucode = reader["fcucode"].ToString(),
                        FAcname = reader["FACNAME"] != DBNull.Value ? reader["FACNAME"].ToString() : null,
                        FWT = reader["FWT"] != DBNull.Value ? Convert.ToDecimal(reader["FWT"]) : 0,
                        FBillAmt = reader["FBILLAMT"] != DBNull.Value ? Convert.ToDecimal(reader["FBILLAMT"]) : 0,
                        FOnline = reader["FONLINE"] != DBNull.Value ? reader["FONLINE"].ToString() : null,
                        FVouchNo = reader["FVOUCHNO"].ToString(),
                        FVouchDt = Convert.ToDateTime(reader["fVouchdt"])
                    });
                }

                return Ok(new
                {
                    success = true,
                    data = result,
                    pagination = new
                    {
                        currentPage = page,
                        pageSize = pageSize,
                        totalCount = totalCount,
                        totalPages = (int)Math.Ceiling((double)totalCount / pageSize)
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }
    }

    // DTO Classes
    public class ChitListDto
    {
        public string FCode { get; set; }
        public string FAcname { get; set; }
    }

    public class SchemeDetailDto
    {
        public string FCode { get; set; }
        public string FAcname { get; set; }
        public string FPhone { get; set; }
        public decimal FAmount { get; set; }
        public string FCompCode { get; set; }
        public DateTime FDate { get; set; }
        public DateTime MaturityDate { get; set; }
        public int FDue { get; set; }
        public string SchemeCode { get; set; }
        public int PaidDue { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal TotalWeight { get; set; }
        public decimal TotalBenefitAmt { get; set; }
        public decimal TotalBenefitWt { get; set; }
        public string SchemeName { get; set; }
        public string ActiveStatus { get; set; }
    }

    public class PaymentDetailDto
    {
        public string FID { get; set; }
        public string FCucode { get; set; }
        public string FAcname { get; set; }
        public decimal FWT { get; set; }
        public decimal FBillAmt { get; set; }
        public string FOnline { get; set; }
        public string FVouchNo { get; set; }
        public DateTime FVouchDt { get; set; }
    }
    
}