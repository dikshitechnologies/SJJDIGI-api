using JEWELLBISREACT.DBConnection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;

namespace CHITSCHEME.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ChitStatusReportController : ControllerBase
    {
        [HttpGet("GetChitStatusReport")]
        public async Task<IActionResult> GetChitStatusReport(
            [FromQuery] string phoneNo,
            [FromQuery] string? schemeCode = null)
        {
            try
            {
                var connectionString = DBHelper.GetConnection();

                using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();

                var query = @"
WITH RankedSchemes AS
(
    SELECT
        P.FCODE,
        P.FACNAME,
        P.FPHONE,
        P.FAMOUNT,
        P.FCOMPCODE,
        P.FDATE,
        P.FDUE,
        P.FID AS SCHEMECODE,

        CASE
            WHEN
            (
                P.FSHOW = '1'
                OR
                (
                    P.FSCHEMETYPE NOT IN ('WT','AT','W')
                    AND P.FDIGITYPE NOT IN ('DS','AT','WT')
                )
            )
            THEN 'Active'
            ELSE 'Inactive'
        END AS ActiveStatus,

        ISNULL(L.MaxDue, 0) AS PaidDue,

        ISNULL(T.TotalAmount, 0) AS TotalAmount,
        ISNULL(T.TotalWeight, 0) AS TotalWeight,

        PARENT.FACNAME AS SCHEMENAME

    FROM PARTY P

    LEFT JOIN
    (
        SELECT
            FID,
            MAX(FDUE) AS MaxDue
        FROM LEDGER
        WHERE
            FCRDB = 'CR'
            AND FTYPE = 'CT'
        GROUP BY FID
    ) L
        ON P.FID = L.FID

    LEFT JOIN
    (
        SELECT
            L.FID,
            SUM(ISNULL(L.FVRAMOUNT,0)) AS TotalAmount,
            SUM(ISNULL(B.FWT,0)) AS TotalWeight
        FROM LEDGER L
        INNER JOIN BLEDGER B
            ON B.FVOUCHNO = L.FVRNO
        WHERE
            L.FCRDB = 'CR'
            AND L.FTYPE = 'CT'
        GROUP BY L.FID
    ) T
        ON P.FID = T.FID

    LEFT JOIN PARTY PARENT
        ON PARENT.FPARENT = LEFT(P.FPARENT, LEN(P.FPARENT) - 5)

    WHERE
        P.FPHONE = @PhoneNo
        AND P.FPARENT LIKE '0000100044%'
        AND (@SchemeCode IS NULL OR @SchemeCode = '' OR P.FID = @SchemeCode)
)

SELECT
    FCODE,
    FACNAME,
    FPHONE,
    FAMOUNT,
    FCOMPCODE,
    FDATE,
    FDUE,
    SCHEMECODE,
    PaidDue,
    TotalAmount,
    TotalWeight,
    SCHEMENAME,
    ActiveStatus
FROM RankedSchemes
ORDER BY FACNAME;";

                using var command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@PhoneNo", phoneNo);
                command.Parameters.AddWithValue("@SchemeCode",
                    string.IsNullOrWhiteSpace(schemeCode) ? DBNull.Value : schemeCode);

                using var reader = await command.ExecuteReaderAsync();

                var list = new List<object>();

                while (await reader.ReadAsync())
                {
                    list.Add(new
                    {
                        FCode = reader["FCODE"]?.ToString(),
                        FACNAME = reader["FACNAME"]?.ToString(),
                        FPHONE = reader["FPHONE"]?.ToString(),
                        FAMOUNT = Convert.ToDecimal(reader["FAMOUNT"]),
                        FCOMPCODE = reader["FCOMPCODE"]?.ToString(),
                        FDATE = Convert.ToDateTime(reader["FDATE"]).ToString("yyyy-MM-dd"),
                        FDUE = Convert.ToInt32(reader["FDUE"]),
                        SCHEMECODE = reader["SCHEMECODE"]?.ToString(),
                        PaidDue = Convert.ToInt32(reader["PaidDue"]),
                        TotalAmount = Convert.ToDecimal(reader["TotalAmount"]).ToString("0.00"),
                        TotalWeight = Convert.ToDecimal(reader["TotalWeight"]).ToString("0.000"),
                        SCHEMENAME = reader["SCHEMENAME"]?.ToString(),
                        ActiveStatus = reader["ActiveStatus"]?.ToString()
                    });
                }

                return Ok(new
                {
                    Success = true,
                    Count = list.Count,
                    Data = list
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    Success = false,
                    Message = ex.Message
                });
            }
        }
    }
}