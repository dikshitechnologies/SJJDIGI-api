using System;
using System.Reflection.PortableExecutable;
using CHITSCHEME.Helpers;
using CHITSCHEME.Models;
using JEWELLBISREACT.DBConnection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Hosting;

namespace CHITSCHEME.Controllers
{
    [Route("api/[controller]")]
    [Authorize] 
    [ApiController]
    public class SchemeDetailsController : ControllerBase
    {
        //        [HttpGet("schemeList")]
        //        public async Task<IActionResult> GetSchemeDetails([FromHeader] string authorization)
        //        {
        //            if (string.IsNullOrEmpty(authorization) || !authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        //            {
        //                return Unauthorized(new { message = "Authorization header is missing or invalid." });
        //            }

        //            var token = authorization.Substring("Bearer ".Length).Trim();
        //            var phone = JwtHelper.GetPhoneFromJwtToken(token);

        //            if (string.IsNullOrEmpty(phone))
        //            {
        //                return Unauthorized(new { message = "Invalid token." });
        //            }

        //            var connectionString = DBHelper.GetConnection();

        //            try
        //            {
        //                using var connection = new SqlConnection(connectionString);
        //                await connection.OpenAsync();

        //                var schemeRate = await GetSchemeRateAsync(connection);

        //                var query = @"

        //WITH RankedSchemes AS (
        //    SELECT 
        //        P.FCODE,
        //        P.FACNAME,
        //        P.FPHONE,
        //        P.FAMOUNT,
        //        P.FCOMPCODE,
        //        P.FDUE,
        //        L.fvrno,
        //        P.FID AS SCHEMECODE,
        //        P.FSCHEMETYPE,
        //        CASE WHEN L.FDUE IS NOT NULL THEN L.FDUE + 1 ELSE 1 END AS PaidDue,
        //        IIF(L.FDUE IS NULL, 'N', IIF(P.FDUE = L.FDUE, 'Y', 'N')) AS FDUE_Comparison,
        //        PARENT.FACNAME AS SCHEMENAME,
        //        ROW_NUMBER() OVER (PARTITION BY P.FID ORDER BY ISNULL(L.FDUE, 0) DESC) AS rn,
        //        CASE 
        //          WHEN EXISTS (
        //            SELECT 1 
        //            FROM LEDGER L3
        //            JOIN BLEDGER B3 ON B3.FVOUCHNO = L3.FVRNO AND B3.FONLINE = 'Y'
        //            WHERE 
        //              L3.FID = P.FID 
        //               AND L3.FDATE BETWEEN DATEFROMPARTS(YEAR(GETDATE()), MONTH(GETDATE()), 1) AND EOMONTH(GETDATE())
        //              AND L3.fCrDb = 'CR' 
        //              AND L3.FTYPE = 'CT'
        //          ) THEN 'Y'
        //          ELSE 'N'
        //        END AS IS_CURRENT_MONTH_PAID
        //    FROM PARTY P
        //    LEFT JOIN (
        // SELECT 
        //            L1.FID,
        //            L1.FVRNO,
        //            L1.FDUE,
        //            L1.FVRAMOUNT
        //        FROM LEDGER L1
        //        INNER JOIN (
        //            SELECT FID, MAX(FVRNO) AS MaxFVRNO
        //            FROM LEDGER L2
        //            JOIN BLEDGER B2 ON B2.FVOUCHNO = L2.FVRNO
        //            WHERE L2.fCrDb = 'CR' AND L2.FTYPE = 'CT' AND B2.FONLINE = 'Y'
        //            GROUP BY L2.FID
        //        ) AS MaxRows ON L1.FID = MaxRows.FID AND L1.FVRNO = MaxRows.MaxFVRNO
        //        JOIN BLEDGER B1 ON B1.FVOUCHNO = L1.FVRNO AND B1.FONLINE = 'Y'
        //        WHERE L1.fCrDb = 'CR' AND L1.FTYPE = 'CT'
        //    ) L ON P.FID = L.FID
        //    LEFT JOIN PARTY PARENT ON PARENT.FPARENT = LEFT(P.FPARENT, LEN(P.FPARENT) - 5)
        //    WHERE P.FPHONE = @phone AND P.FPARENT LIKE '0000100044%'
        //)
        //SELECT *
        //FROM RankedSchemes
        //WHERE rn = 1;

        //";

        //                using var command = new SqlCommand(query, connection);
        //                command.Parameters.AddWithValue("@phone", phone);

        //                using var reader = await command.ExecuteReaderAsync();

        //                if (!reader.HasRows)
        //                {
        //                    return NotFound(new { message = "No Scheme found for the Provided PhoneNo." });
        //                }

        //                var response = new SchemeResponse();
        //                response.schemeDetails = new List<SchemeInfo>();

        //                while (await reader.ReadAsync())
        //                {
        //                    if (response.fAcname == null)
        //                    {
        //                        response.fAcname = reader["FACNAME"]?.ToString();
        //                        response.fphone = reader["FPHONE"]?.ToString();
        //                        response.fCompCode = reader["FCOMPCODE"]?.ToString();
        //                        response.GolRateAmt = schemeRate.ToString();
        //                    }

        //                    if (reader["FDUE_Comparison"]?.ToString() == "Y")
        //                    {
        //                        continue;
        //                    }

        //                    var fSchemeType = reader["FSCHEMETYPE"]?.ToString();
        //                    string famount;
        //                    string weight = null;

        //                    if (fSchemeType == "W")
        //                    {
        //                        var payAmount = Convert.ToDecimal(reader["famount"] ?? 0);
        //                        var goldRate = schemeRate;
        //                        decimal calculatedWeight = 0;

        //                        if (goldRate != 0)
        //                        {
        //                            calculatedWeight = payAmount / goldRate;
        //                        }

        //                        famount = payAmount.ToString("0.00");
        //                        weight = calculatedWeight.ToString("0.000");
        //                    }
        //                    else
        //                    {
        //                        famount = reader["FAMOUNT"]?.ToString();
        //                        weight = null;
        //                    }

        //                    response.schemeDetails.Add(new SchemeInfo
        //                    {
        //                        fcode = reader["FCODE"]?.ToString(),
        //                        SchemeName = reader["SCHEMENAME"]?.ToString(),
        //                        famount = famount,
        //                        Weight = weight,
        //                        goldrate= schemeRate.ToString(),
        //                        SchemeCode = reader["SCHEMECODE"]?.ToString(),
        //                        TotalDue = reader["FDUE"]?.ToString(),
        //                        PaidDue = reader["PaidDue"]?.ToString(),
        //                        FDUE_Comparison = reader["FDUE_Comparison"]?.ToString(),
        //                        IS_CURRENT_MONTH_PAID = reader["IS_CURRENT_MONTH_PAID"]?.ToString(),

        //                    });
        //                }


        //                return Ok(new
        //                {
        //                    // Common info (customer name, phone, etc.)
        //                    common = new
        //                    {
        //                        facname = response.fAcname,
        //                        fphone = response.fphone,
        //                        fcompcode = response.fCompCode,
        //                        goldRateAmt = response.GolRateAmt
        //                    },

        //                    // Example: Division Names or Live Rates (if you have them)
        //                    divisionNames = new
        //                    {
        //                        gold22K = schemeRate, // map actual division rate
        //                        gold24K = schemeRate, // adjust accordingly
        //                        silver = 0            // replace with actual silver rate
        //                    },

        //                    // Now split schemes into categories
        //                    ch = response.schemeDetails
        //          .Where(s => s.SchemeCode.StartsWith("CH")) // example filter for chit
        //          .Select(s => new
        //          {
        //              fcode = s.fcode,
        //              schemename = s.SchemeName,
        //              famount = s.famount,
        //              totalGrams = s.Weight,
        //              totalAmount = s.famount,
        //              schemecode = s.SchemeCode,
        //              paidDue = s.PaidDue,
        //              fdue = s.TotalDue,
        //              iS_CURRENT_MONTH_PAID = s.IS_CURRENT_MONTH_PAID,
        //              fcompcode = s.goldrate
        //          }),

        //                    dG22K = response.schemeDetails
        //          .Where(s => s.SchemeCode.Contains("22K")) // example filter for 22K
        //          .Select(s => new
        //          {
        //              fcode = s.fcode,
        //              schemename = s.SchemeName,
        //              famount = s.famount,
        //              totalGrams = s.Weight,
        //              totalAmount = s.famount,
        //              schemecode = s.SchemeCode,
        //              paidDue = s.PaidDue,
        //              fdue = s.TotalDue,
        //              iS_CURRENT_MONTH_PAID = s.IS_CURRENT_MONTH_PAID,
        //              fcompcode = s.goldrate
        //          }),

        //                    dG24K = response.schemeDetails
        //          .Where(s => s.SchemeCode.Contains("24K")) // example filter for 24K
        //          .Select(s => new
        //          {
        //              fcode = s.fcode,
        //              schemename = s.SchemeName,
        //              famount = s.famount,
        //              totalGrams = s.Weight,
        //              totalAmount = s.famount,
        //              schemecode = s.SchemeCode,
        //              paidDue = s.PaidDue,
        //              fdue = s.TotalDue,
        //              iS_CURRENT_MONTH_PAID = s.IS_CURRENT_MONTH_PAID,
        //              fcompcode = s.goldrate
        //          }),

        //                    silver = response.schemeDetails
        //          .Where(s => s.SchemeCode.Contains("SILVER")) // example filter for silver
        //          .Select(s => new
        //          {
        //              fcode = s.fcode,
        //              schemename = s.SchemeName,
        //              famount = s.famount,
        //              totalGrams = s.Weight,
        //              totalAmount = s.famount,
        //              schemecode = s.SchemeCode,
        //              paidDue = s.PaidDue,
        //              fdue = s.TotalDue,
        //              iS_CURRENT_MONTH_PAID = s.IS_CURRENT_MONTH_PAID,
        //              fcompcode = s.goldrate
        //          })
        //                });

        //            }
        //            catch (SqlException)
        //            {
        //                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Database error. Please try again later." });
        //            }
        //            catch (Exception)
        //            {
        //                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An unexpected error occurred. Please try again later." });
        //            }
        //        }


        public class CommonDto
        {
            public string FacName { get; set; }
            public string FPhone { get; set; }
            public string FCompCode { get; set; }
            public string GolRateAmt { get; set; }
        }

        public class DivisionRateDto
        {
            public string FCode { get; set; }
            public string FName { get; set; }
            public string FRate { get; set; }
        }


        [HttpGet("schemeList")]
        public async Task<IActionResult> GetSchemeDetails([FromHeader] string authorization)
        {
            if (string.IsNullOrEmpty(authorization) || !authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                return Unauthorized(new { message = "Authorization header is missing or invalid." });
            }

            var token = authorization.Substring("Bearer ".Length).Trim();
            var phone = JwtHelper.GetPhoneFromJwtToken(token);

            if (string.IsNullOrEmpty(phone))
            {
                return Unauthorized(new { message = "Invalid token." });
            }

            var connectionString = DBHelper.GetConnection();

            try
            {
                using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();

                // ✅ 1. Fetch Division Names (this already gives you 22K & 24K rates)
                var divisionQuery = "SELECT fcode, fName, frate FROM Division WHERE fcode IN ('0002','0003','0005',0004)";
                var divisionNames = new List<object>();

                using (var divCmd = new SqlCommand(divisionQuery, connection))
                using (var divReader = await divCmd.ExecuteReaderAsync())
                {
                    while (await divReader.ReadAsync())
                    {
                        divisionNames.Add(new
                        {
                            fcode = divReader["fcode"]?.ToString(),
                            fname = divReader["fName"]?.ToString(),
                            fRate = divReader["frate"]?.ToString()
                        });
                    }
                }

                // ✅ 2. Fetch Schemes
                var schemeQuery = @"WITH RankedSchemes AS (
            SELECT 
                P.FCODE,
                P.FACNAME,
                P.FPHONE,
                P.FAMOUNT,
                P.FCOMPCODE,
                P.FDUE,
                P.FDIGICR,
                P.FDIGITYPE,
                P.FID AS SCHEMECODE,
                P.FSCHEMETYPE,
                CASE WHEN L.FDUE IS NOT NULL THEN L.FDUE + 1 ELSE 1 END AS PaidDue,
                IIF(L.FDUE IS NULL, 'N', IIF(P.FDUE = L.FDUE, 'Y', 'N')) AS FDUE_Comparison,
                PARENT.FACNAME AS SCHEMENAME,
                ROW_NUMBER() OVER (PARTITION BY P.FID ORDER BY ISNULL(L.FDUE, 0) DESC) AS rn,
                CASE 
                  WHEN EXISTS (
                    SELECT 1 
                    FROM LEDGER L3
                    JOIN BLEDGER B3 ON B3.FVOUCHNO = L3.FVRNO AND B3.FONLINE = 'Y'
                    WHERE 
                      L3.FID = P.FID 
                       AND L3.FDATE BETWEEN DATEFROMPARTS(YEAR(GETDATE()), MONTH(GETDATE()), 1) AND EOMONTH(GETDATE())
                      AND L3.fCrDb = 'CR' 
                      AND L3.FTYPE = 'CT'
                  ) THEN 'Y'
                  ELSE 'N'
                END AS IS_CURRENT_MONTH_PAID
            FROM PARTY P
            LEFT JOIN (
                SELECT 
                    L1.FID,
                    L1.FVRNO,
                    L1.FDUE,
                    L1.FVRAMOUNT
                FROM LEDGER L1
                INNER JOIN (
                    SELECT FID, MAX(FVRNO) AS MaxFVRNO
                    FROM LEDGER L2
                    JOIN BLEDGER B2 ON B2.FVOUCHNO = L2.FVRNO
                    WHERE L2.fCrDb = 'CR' AND L2.FTYPE = 'CT' AND B2.FONLINE = 'Y'
                    GROUP BY L2.FID
                ) AS MaxRows ON L1.FID = MaxRows.FID AND L1.FVRNO = MaxRows.MaxFVRNO
                JOIN BLEDGER B1 ON B1.FVOUCHNO = L1.FVRNO AND B1.FONLINE = 'Y'
                WHERE L1.fCrDb = 'CR' AND L1.FTYPE = 'CT'
            ) L ON P.FID = L.FID
            LEFT JOIN PARTY PARENT ON PARENT.FPARENT = LEFT(P.FPARENT, LEN(P.FPARENT) - 5)
            WHERE P.FPHONE = @phone AND P.FPARENT LIKE '0000100044%'
        )
        SELECT *
        FROM RankedSchemes
        WHERE rn = 1;";

                CommonDto common = null;
                var chList = new List<object>();
                var dg22kList = new List<object>();
                var dg24kList = new List<object>();
                var silverList = new List<object>();

                using var cmd = new SqlCommand(schemeQuery, connection);
                cmd.Parameters.AddWithValue("@phone", phone);

                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    if (common == null && reader["FACNAME"] != DBNull.Value)
                    {
                        common = new CommonDto
                        {
                            FacName = reader["FACNAME"]?.ToString(),
                            FPhone = reader["FPHONE"]?.ToString(),
                            FCompCode = reader["FCOMPCODE"]?.ToString(),
                            GolRateAmt = null // ❌ removed schemeRate (you can drop this property if unused)
                        };
                    }

                    var scheme = new
                    {
                        fcode = reader["FCODE"]?.ToString(),
                        schemename = reader["SCHEMENAME"]?.ToString(),
                        famount = reader["FAMOUNT"]?.ToString(),
                        schemecode = reader["SCHEMECODE"]?.ToString(),
                        totalDue = reader["FDUE"]?.ToString(),
                        paidDue = reader["PaidDue"]?.ToString(),
                        fdue_comparison = reader["FDUE_Comparison"]?.ToString(),
                        iS_CURRENT_MONTH_PAID = reader["IS_CURRENT_MONTH_PAID"]?.ToString(),
                        fdigicr = reader["FDIGICR"]?.ToString(),
                        fcompcode = reader["FCOMPCODE"]?.ToString()
                    };

                    var digiType = reader["FDIGITYPE"]?.ToString();

                    if (digiType == "CH") chList.Add(scheme);
                    else if (digiType == "DG" && scheme.fdigicr == "22K") dg22kList.Add(scheme);
                    else if (digiType == "DG" && scheme.fdigicr == "24K") dg24kList.Add(scheme);
                    else if (digiType == "DS") silverList.Add(scheme);
                }

                return Ok(new
                {
                    common,
                    divisionNames,
                    ch = chList,
                    dG22K = dg22kList,
                    dG24K = dg24kList,
                    silver = silverList
                });
            }
            catch (SqlException)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Database error. Please try again later." });
            }
            catch (Exception)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An unexpected error occurred. Please try again later." });
            }
        }



        [HttpGet("getCustomerScheme")]
        public async Task<IActionResult> GetCustomerScheme([FromHeader] string authorization)
        {

            if (string.IsNullOrEmpty(authorization) || !authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                return Unauthorized(new { message = "Authorization header is missing or invalid." });
            }

            var token = authorization.Substring("Bearer ".Length).Trim();
            var phone = JwtHelper.GetPhoneFromJwtToken(token);

            if (string.IsNullOrEmpty(phone))
            {
                return Unauthorized(new { message = "Invalid token." });
            }
            var result = new List<object>();

            string query = @"
        SELECT 
            P.FCODE AS CUSCODE,
            P.FACNAME AS CUSNAME,
            P.FID AS SCHEMECODE,
            PARENT.FACNAME  AS SCHEMENAME
        FROM PARTY P
        LEFT JOIN PARTY PARENT 
            ON PARENT.FPARENT = LEFT(P.FPARENT, LEN(P.FPARENT) - 5)
        WHERE P.FPHONE = @phone 
            AND P.FPARENT LIKE '0000100044%'"
            ;
            //0287102872

            try
            {
                using (SqlConnection con = new SqlConnection(DBHelper.GetConnection()))
                using (SqlCommand cmd = new SqlCommand(query, con))
                {

                    cmd.Parameters.AddWithValue("@phone", phone);

                    await con.OpenAsync();
                    using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            result.Add(new
                            {
                                CusCode = reader["CUSCODE"]?.ToString(),
                                CUSNAME = reader["CUSNAME"]?.ToString(),
                                SchemeCode = reader["SCHEMECODE"]?.ToString(),
                                SCHEMENAME = reader["SCHEMENAME"]?.ToString(),
                            });
                        }
                    }
                }

                return Ok(result);
            }
            catch (SqlException)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Database error. Please try again later." });
            }
            catch (Exception)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An unexpected error occurred. Please try again later." });
            }
        }





        private async Task<decimal> GetSchemeRateAsync(SqlConnection connection)
        {
            var query = @"SELECT FSCHEMERATE FROM RateFix WHERE 1=1";
            using var cmd = new SqlCommand(query, connection);
            var result = await cmd.ExecuteScalarAsync();
            if (result != null && decimal.TryParse(result.ToString(), out decimal rate))
            {
                return rate;
            }
            return 0;
        }




        [HttpGet("SchemeReport/{SchemeId}")]
        public async Task<IActionResult> GetLedgerDetails(string SchemeId)
        {
            var ledgerDetails = new List<LedgerDetails>();
            decimal ledgerDue = 0;
            decimal partyTotalDue = 0;

            try
            {
                using (var connection = new SqlConnection(DBHelper.GetConnection()))
                {
                    await connection.OpenAsync();

                    var ledgerQuery = @"
                

            SELECT 
                FORMAT(CAST(L.FDATE AS DATE), 'dd/MM/yyyy') AS FDATE,
                L.FVRAMOUNT,
                L.FWT
            FROM LEDGER L
            JOIN PARTY P ON P.FID = L.FID
            JOIN BLEDGER B ON B.FVOUCHNO = L.FVRNO AND B.FONLINE = 'Y'
            WHERE 
                L.FID = @FID 
                AND L.FCRDB = 'CR' 
                AND L.FTYPE = 'CT';

            ";

                    using (var command = new SqlCommand(ledgerQuery, connection))
                    {
                        command.Parameters.AddWithValue("@FID", SchemeId);

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                var details = new LedgerDetails
                                {
                                    FDATE = reader["FDATE"].ToString(),
                                    FVRAMOUNT = reader["FVRAMOUNT"].ToString(),
                                    FWT = reader["FWT"].ToString()
                                };

                                ledgerDetails.Add(details);
                            }
                        }
                    }

                    // 2. Fetch Latest Due Information
                    var dueQuery = @"
                


               
                   SELECT TOP 1
                     ISNULL(L.FDUE, 0) AS FDUE,  
                    P.FDUE AS PartyFDUE, 
                    L.FVRNO
                FROM PARTY P
                LEFT JOIN LEDGER L 
                    ON L.FID = P.FID AND L.FCRDB = 'CR'
                LEFT JOIN BLEDGER B 
                    ON B.FVOUCHNO = L.FVRNO AND B.FONLINE = 'Y'
                WHERE P.FID =  @FID
                ORDER BY L.FDUE DESC
                    ";

                    using (var dueCommand = new SqlCommand(dueQuery, connection))
                    {
                        dueCommand.Parameters.AddWithValue("@FID", SchemeId);

                        using (var dueReader = await dueCommand.ExecuteReaderAsync())
                        {
                            if (await dueReader.ReadAsync())
                            {
                                ledgerDue = dueReader["FDUE"] != DBNull.Value ? Convert.ToDecimal(dueReader["FDUE"]) : 0;
                                partyTotalDue = dueReader["PartyFDUE"] != DBNull.Value ? Convert.ToDecimal(dueReader["PartyFDUE"]) : 0;
                            }
                        }
                    }
                }

                // 3. Return combined result
                return Ok(new
                {
                    LedgerDetails = ledgerDetails,
                    LedgerDue = ledgerDue,
                    PartyTotalDue = partyTotalDue
                });
            }
            catch (SqlException sqlEx)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Database error. Please try again later." });
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An unexpected error occurred. Please try again later." });
            }
        }






        //----------------------------------------------------post method-------------------------------------------------


        static string gobaldatacode;
        public static string GetSingleChitSchemeVoucherNo(SqlConnection conn, SqlTransaction transaction)
        {
            string query = "SELECT MAX(fVouchno) FROM Bledger WHERE fbILLType = 'CT' and FONLINE = 'Y'";
            int startNumber = 1;

            using var cmd = new SqlCommand(query, conn, transaction);
            var result = cmd.ExecuteScalar();

            if (result != null && result != DBNull.Value && !string.IsNullOrWhiteSpace(result.ToString()))
            {
                string currentVouchNo = result.ToString();
                string numericPart = new string(currentVouchNo.Where(char.IsDigit).ToArray());

                if (!string.IsNullOrEmpty(numericPart) && int.TryParse(numericPart, out int number))
                {
                    startNumber = number + 1;
                }
            }

            string paddedNumber = startNumber.ToString("D5"); // 5 digits with padding
            gobaldatacode = paddedNumber;
            return paddedNumber;
        }








        //---------------- Duplicate Voucher No Checking ------------------
        private bool SchemeNameExists(SqlConnection con, SqlTransaction transaction, string voucherNo)
        {
            using (SqlCommand cmd = new SqlCommand("SELECT 1 FROM Bledger WHERE fVouchno = @fVouchno", con, transaction))
            {
                cmd.Parameters.AddWithValue("@fVouchno", voucherNo);
                return cmd.ExecuteScalar() != null;
            }
        }


        [HttpPost("InsertChitScheme")]
        public IActionResult InsertChitScheme([FromBody] ChitSchemeModel model)
        {


            try
            {
                using (SqlConnection conn = new SqlConnection(DBHelper.GetConnection()))
                {
                    conn.Open();
                    SqlTransaction transaction = conn.BeginTransaction();

                    try
                    {

                        //var voucherNos = GetChitSchemeVoucherNos(conn, transaction, model.SchemeDetails.Count);
                        string voucherNo = GetSingleChitSchemeVoucherNo(conn, transaction);


                        //foreach (var voucherNo in voucherNos)
                        //{
                            //if (SchemeNameExists(conn, transaction, voucherNo))
                            //{
                            //    return Conflict(new { message = $"Voucher number {voucherNo} already exists. Please choose a different one." });
                            //}
                        //}


                        InsertBledger(model.SchemeDetails, voucherNo, conn, transaction);
                        InsertLedger(model.SchemeDetails, voucherNo, conn, transaction);
                        transaction.Commit();
                        return Ok(new
                        {
                            Message = "Insert successful.",
                            VoucherNo = voucherNo
                        });
                    }
                    catch (SqlException sqlEx)
                    {
                        transaction.Rollback();
                        return StatusCode(500, $"Database error: {sqlEx.Message}");
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        return StatusCode(500, $"Insert failed: {ex.Message}");
                    }
                }
            }
            catch (Exception outerEx)
            {
                return StatusCode(500, $"Unexpected error: {outerEx.Message}");
            }
        }


      
        private static void InsertBledger(List<SchemeList> schemeList, string voucherNo, SqlConnection conn, SqlTransaction transaction)
        {
            if (schemeList.Count > 0)  // Ensure there's at least one item in the list
            {
                string insertBledger = @"
        INSERT INTO Bledger 
        (fCucode, fvType, fVouchno, fVouchdt, fBillAmt, fBalAmt, fBillType, fUser, fCompCode, FSTAT, FREFNO, FPAYMODE, FCASH, FSMSSALES, FSMSCHIT, FINT, fwt, FRATE, FCARD, FUPI, FNEFT, FCHQ, FONLINE, fOpCode, FCARDCODE, FNEFTCODE, FNARRATION, FCHQCODE,FUPICODE)
        VALUES 
        (@fCucode, @fvType, @fVouchno, @fVouchdt, @fBillAmt, @fBalAmt, @fBillType, @fUser, @fCompCode, @FSTAT, @FREFNO, @FPAYMODE, @FCASH, @FSMSSALES, @FSMSCHIT, @FINT, @fwt, @FRATE, @FCARD, @FUPI, @FNEFT, @FCHQ, @FONLINE,@fOpCode,@FCARDCODE,@FNEFTCODE,@FNARRATION,@FCHQCODE,@FUPICODE)";

                var item = schemeList[0]; // Access the first item in the list

                using (SqlCommand cmd = new SqlCommand(insertBledger, conn, transaction))
                {
                    cmd.Parameters.AddWithValue("@fCucode", item.CusCode);
                    cmd.Parameters.AddWithValue("@fvType", "CT");
                    cmd.Parameters.AddWithValue("@fVouchno", voucherNo); // SAME voucher
                    cmd.Parameters.AddWithValue("@fVouchdt", DateTime.Now.Date);
                    cmd.Parameters.AddWithValue("@fBillAmt", item.TotalAmt);
                    cmd.Parameters.AddWithValue("@fBalAmt", item.TotalAmt);
                    cmd.Parameters.AddWithValue("@fBillType", "CT");
                    cmd.Parameters.AddWithValue("@fUser", item.CompCode);
                    cmd.Parameters.AddWithValue("@fCompCode", item.CompCode);
                    cmd.Parameters.AddWithValue("@FSTAT", "N");
                    cmd.Parameters.AddWithValue("@FREFNO", gobaldatacode);
                    cmd.Parameters.AddWithValue("@FPAYMODE", "UPI");
                    cmd.Parameters.AddWithValue("@FCASH", "0");
                    cmd.Parameters.AddWithValue("@FSMSSALES", "N");
                    cmd.Parameters.AddWithValue("@FSMSCHIT", "N");
                    cmd.Parameters.AddWithValue("@FINT", "0");
                    cmd.Parameters.AddWithValue("@fwt", "0");
                    cmd.Parameters.AddWithValue("@FRATE", "0");
                    cmd.Parameters.AddWithValue("@FCARD", "0");
                    cmd.Parameters.AddWithValue("@FUPI", item.Amount);
                    cmd.Parameters.AddWithValue("@FNEFT", "0");
                    cmd.Parameters.AddWithValue("@FCHQ", "0");
                    cmd.Parameters.AddWithValue("@FONLINE", "Y");
                    cmd.Parameters.AddWithValue("@fOpCode", "");
                    cmd.Parameters.AddWithValue("@FCARDCODE", "");
                    cmd.Parameters.AddWithValue("@FNEFTCODE", "");
                    cmd.Parameters.AddWithValue("@FNARRATION", "");
                    cmd.Parameters.AddWithValue("@FCHQCODE", "");
                    cmd.Parameters.AddWithValue("@FUPICODE", "00068");
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private static void InsertLedger(List<SchemeList> schemeList, string voucherNo, SqlConnection conn, SqlTransaction transaction)
        {
            string insertLedger = @"
    INSERT INTO Ledger 
    (faccode, fvrno, fType, fDate, fCrDb, fCaCb, fvrAmount, fRefcode, fCompCode, fRefNo, fid, FDUE, FPRINT, fNarration, FMOP,fwt)
    VALUES 
    (@faccode, @fvrno, @fType, @fDate, @fCrDb, @fCaCb, @fvrAmount, @fRefcode, @fCompCode, @fRefNo, @fid, @FDUE, @FPRINT, @fNarration, @FMOP,@fwt)";

            if (schemeList.Count > 0)
            {
                var firstItem = schemeList[0];

                // Insert DR entry
                using (SqlCommand cmd = new SqlCommand(insertLedger, conn, transaction))
                {
                    cmd.Parameters.AddWithValue("@faccode", "00068");
                    cmd.Parameters.AddWithValue("@fvrno", voucherNo);
                    cmd.Parameters.AddWithValue("@fType", "CT");
                    cmd.Parameters.AddWithValue("@fDate", DateTime.Now.Date);
                    cmd.Parameters.AddWithValue("@fCrDb", "DR");
                    cmd.Parameters.AddWithValue("@fCaCb", "D");
                    cmd.Parameters.AddWithValue("@fvrAmount", firstItem.TotalAmt);
                    cmd.Parameters.AddWithValue("@fRefcode", firstItem.CusCode);
                    cmd.Parameters.AddWithValue("@fCompCode", firstItem.CompCode);
                    cmd.Parameters.AddWithValue("@fRefNo", DBNull.Value);
                    cmd.Parameters.AddWithValue("@fid", firstItem.SchemeCode);
                    cmd.Parameters.AddWithValue("@FDUE", firstItem.FDUE);
                    cmd.Parameters.AddWithValue("@FPRINT", "N");
                    cmd.Parameters.AddWithValue("@fNarration", "");
                    cmd.Parameters.AddWithValue("@FMOP", "UPI");
                    cmd.Parameters.AddWithValue("@fwt", firstItem.Weight ?? (object)DBNull.Value);
                    cmd.ExecuteNonQuery();
                }
            }

            // Insert CR entries
            foreach (var item in schemeList)
            {
                using (SqlCommand cmd = new SqlCommand(insertLedger, conn, transaction))
                {
                    cmd.Parameters.AddWithValue("@faccode", "00045");
                    cmd.Parameters.AddWithValue("@fvrno", voucherNo); // SAME voucher
                    cmd.Parameters.AddWithValue("@fType", "CT");
                    cmd.Parameters.AddWithValue("@fDate", DateTime.Now.Date);
                    cmd.Parameters.AddWithValue("@fCrDb", "CR");
                    cmd.Parameters.AddWithValue("@fCaCb", "C");
                    cmd.Parameters.AddWithValue("@fvrAmount", item.Amount);
                    cmd.Parameters.AddWithValue("@fRefcode", "00068");
                    cmd.Parameters.AddWithValue("@fCompCode", item.CompCode);
                    cmd.Parameters.AddWithValue("@fRefNo", item.SchemeCode);
                    cmd.Parameters.AddWithValue("@fid", item.SchemeCode);
                    cmd.Parameters.AddWithValue("@FDUE", item.FDUE);
                    cmd.Parameters.AddWithValue("@FPRINT", "N");
                    cmd.Parameters.AddWithValue("@fNarration", "");
                    cmd.Parameters.AddWithValue("@FMOP", "UPI");
                    cmd.Parameters.AddWithValue("@fwt", item.Weight ?? (object)DBNull.Value);
                    cmd.ExecuteNonQuery();
                }
            }
        }


    }
}







public class SchemeResponse
{
    public string fAcname { get; set; }
    public string fphone { get; set; }
    public string fCompCode { get; set; }
    public string GolRateAmt { get; set; }
    public List<SchemeInfo> schemeDetails { get; set; }
}

public class SchemeInfo
{
    public string fcode { get; set; }
    public string goldrate { get; set; }
    public string SchemeName { get; set; }
    public string famount { get; set; }
    public string SchemeCode { get; set; }
    public string TotalDue { get; set; }
    public string PaidDue { get; set; }
    public string FDUE_Comparison { get; set; }
    public string Weight { get; set; }
    public string IS_CURRENT_MONTH_PAID { get; set; }
    public string fdigicr { get; set; }
}


public class LedgerDetails
{
    public string FDATE { get; set; }
    public string FVRAMOUNT { get; set; }
    public string FWT { get; set; }
}