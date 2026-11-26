
using JEWELLBISREACT.DBConnection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;

namespace CHITSCHEME_SJJDIGI.Controllers.Reports
{
    [Route("api/[controller]")]
    [ApiController]
    public class DaywiseActivityController : ControllerBase
    {
        private readonly string connectionString;

        public DaywiseActivityController(IConfiguration? configuration = null)
        {
            connectionString = configuration?.GetConnectionString("DefaultConnection")
                ?? DBHelper.GetConnection();
        }

        // ============================================
        // DTO 1: Daywise Customer Details
        // ============================================

        public class DaywiseCustomerDto
        {
            public string? fCode { get; set; }
            public string? fAcname { get; set; }
            public string? fPhone { get; set; }
            public DateTime? FDATE { get; set; }
            public string? FID { get; set; }
            public string? fDigiType { get; set; }
            public string? fParent { get; set; }
            public string? ParentName { get; set; }
            public decimal? fAmount { get; set; }
            public decimal? fDue { get; set; }
        }

        // ============================================
        // DTO 2: Parent Master List
        // ============================================

        public class ParentListDto
        {
            public string? fCode { get; set; }
            public string? fAcname { get; set; }
            public string? fParent { get; set; }
            public decimal? fAmount { get; set; }
            public decimal? fDue { get; set; }
            public string? fDigiType { get; set; }
        }

        // ============================================
        // API 1: DAYWISE CUSTOMER LIST
        // ============================================

        [HttpGet("GetDaywiseCustomers")]
        public async Task<IActionResult> GetDaywiseCustomers(
            [FromQuery] DateTime fromDate,
            [FromQuery] DateTime toDate,
            [FromQuery] string parentCode = "0000100044",
            [FromQuery] string filterType = "ALL")
        {
            try
            {
                filterType = filterType.Trim().ToUpper();

                if (filterType != "ALL" &&
                    filterType != "DIGI" &&
                    filterType != "SCHEME")
                {
                    return BadRequest(new
                    {
                        status = false,
                        message = "Invalid filterType. Use ALL, DIGI or SCHEME."
                    });
                }

                if (fromDate.Date > toDate.Date)
                {
                    return BadRequest(new
                    {
                        status = false,
                        message = "FromDate cannot be greater than ToDate."
                    });
                }

                var result = new List<DaywiseCustomerDto>();

                using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();

                string query = @"
                    SELECT
                        C.fCode,
                        C.fAcname,
                        C.fPhone,
                        C.FDATE,
                        C.FID,
                        C.fDigiType,
                        C.fParent,
                        P.fAcname AS ParentName,
                        C.fAmount,
                        C.fDue
                    FROM Party C

                    LEFT JOIN Party P
                        ON P.fCode = RIGHT(C.fParent, 5)
                        AND P.fAclevel > 0
                        AND P.fParent LIKE @ParentCode + '%'

                    WHERE
                        C.fAclevel < 0
                        AND C.fParent LIKE @ParentCode + '%'

                        AND C.FDATE >= @FromDate
                        AND C.FDATE < DATEADD(DAY, 1, @ToDate)

                        AND
                        (
                            @FilterType = 'ALL'

                            OR
                            (
                                @FilterType = 'DIGI'
                                AND C.fDigiType IN ('DG', 'DS')
                            )

                            OR
                            (
                                @FilterType = 'SCHEME'
                                AND C.fDigiType NOT IN ('DG', 'DS')
                            )
                        )

                    ORDER BY C.FDATE DESC, C.fCode;
                ";

                using var command = new SqlCommand(query, connection);

                command.Parameters.Add("@FromDate", SqlDbType.Date).Value =
                    fromDate.Date;

                command.Parameters.Add("@ToDate", SqlDbType.Date).Value =
                    toDate.Date;

                command.Parameters.Add("@ParentCode", SqlDbType.VarChar, 30).Value =
                    parentCode;

                command.Parameters.Add("@FilterType", SqlDbType.VarChar, 20).Value =
                    filterType;

                using var reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    result.Add(new DaywiseCustomerDto
                    {
                        fCode = reader["fCode"]?.ToString(),
                        fAcname = reader["fAcname"]?.ToString(),
                        fPhone = reader["fPhone"]?.ToString(),

                        FDATE = reader["FDATE"] == DBNull.Value
                            ? null
                            : Convert.ToDateTime(reader["FDATE"]),

                        FID = reader["FID"]?.ToString(),
                        fDigiType = reader["fDigiType"]?.ToString(),
                        fParent = reader["fParent"]?.ToString(),
                        ParentName = reader["ParentName"]?.ToString(),

                        fAmount = reader["fAmount"] == DBNull.Value
                            ? null
                            : Convert.ToDecimal(reader["fAmount"]),

                        fDue = reader["fDue"] == DBNull.Value
                            ? null
                            : Convert.ToDecimal(reader["fDue"])
                    });
                }

                return Ok(new
                {
                    status = true,
                    message = "Daywise customer list fetched successfully.",
                    totalCount = result.Count,
                    data = result
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    status = false,
                    message = ex.Message
                });
            }
        }

        // ============================================
        // API 2: PARENT MASTER LIST
        // DIGI / SCHEME
        // ============================================

        [HttpGet("GetParentList")]
        public async Task<IActionResult> GetParentList(
            [FromQuery] string parentCode = "0000100044",
            [FromQuery] string filterType = "DIGI")
        {
            try
            {
                filterType = filterType.Trim().ToUpper();

                if (filterType != "DIGI" && filterType != "SCHEME")
                {
                    return BadRequest(new
                    {
                        status = false,
                        message = "Invalid filterType. Use DIGI or SCHEME."
                    });
                }

                var result = new List<ParentListDto>();

                using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();

                string query = @"
                    SELECT
                        fCode,
                        fAcname,
                        fParent,
                        fAmount,
                        fDue,
                        fDigiType
                    FROM Party
                    WHERE
                        fAclevel > 0
                        AND fParent LIKE @ParentCode + '%'

                        AND
                        (
                            (
                                @FilterType = 'DIGI'
                                AND fCode IN ('00051', '00052')
                            )

                            OR

                            (
                                @FilterType = 'SCHEME'
                                AND fCode NOT IN ('00044', '00051', '00052')
                            )
                        )

                    ORDER BY fAcname;
                ";

                using var command = new SqlCommand(query, connection);

                command.Parameters.Add("@ParentCode", SqlDbType.VarChar, 30).Value =
                    parentCode;

                command.Parameters.Add("@FilterType", SqlDbType.VarChar, 20).Value =
                    filterType;

                using var reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    result.Add(new ParentListDto
                    {
                        fCode = reader["fCode"]?.ToString(),
                        fAcname = reader["fAcname"]?.ToString(),
                        fParent = reader["fParent"]?.ToString(),

                        fAmount = reader["fAmount"] == DBNull.Value
                            ? null
                            : Convert.ToDecimal(reader["fAmount"]),

                        fDue = reader["fDue"] == DBNull.Value
                            ? null
                            : Convert.ToDecimal(reader["fDue"]),

                        fDigiType = reader["fDigiType"]?.ToString()
                    });
                }

                return Ok(new
                {
                    status = true,
                    message = "Parent list fetched successfully.",
                    totalCount = result.Count,
                    data = result
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    status = false,
                    message = ex.Message
                });
            }
        }
    }
}
