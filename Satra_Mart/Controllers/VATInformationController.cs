using Dapper;
using Microsoft.AspNetCore.Mvc;
using Satra_Mart;
using System.Data.SqlClient;
using System.Globalization;

namespace Satra_Mart.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class VATInformationController : ControllerBase
    {
        private readonly string _connString;

        public VATInformationController(IConfiguration configuration)
        {
            _connString = configuration.GetConnectionString("DefaultConnection");
        }

        [HttpGet("get")]
        public IActionResult GetTop100()
        {
            try
            {
                using (var conn = new SqlConnection(_connString))
                {
                    conn.Open();
                    var query = @"SELECT TOP (1000) [COMBINATION], [CUSTREQUEST], [FORMFORMAT], [FORMNUM], [INVOICEDATE],
                                          [INVOICENUM], [PURCHASERNAME], [RETAILTRANSACTIONTABLE], [RETAILTRANSRECIDGROUP],
                                          [SERIALNUM], [TAXCOMPANYADDRESS], [TAXCOMPANYNAME], [TAXREGNUM], [TAXTRANSTXT],
                                          [TRANSTIME], [DATAAREAID], [RECVERSION], [PARTITION], [RECID], [EMAIL],
                                          [PHONE], [CUSTACCOUNT], [CANCEL]
                                 FROM [dbo].[VASRetailTransVATInformation]";
                    var result = conn.Query<VATInformation>(query).AsList();
                    return Ok(result);
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        [HttpGet("details/{receiptId}")]
        public IActionResult GetByReceiptId(string receiptId)
        {
            if (string.IsNullOrWhiteSpace(receiptId))
            {
                return BadRequest(new { status = "Error", message = "A valid ReceiptID must be provided." });
            }

            try
            {
                using (var conn = new SqlConnection(_connString))
                {
                    conn.Open();

                    var recIdQuery = @"SELECT TOP 1 RECID 
                               FROM [dbo].[RETAILTRANSACTIONTABLE]
                               WHERE RECEIPTID = @ReceiptId";

                    var realRecid = conn.ExecuteScalar<long?>(recIdQuery, new { ReceiptId = receiptId });

                    if (realRecid == null)
                    {
                        return NotFound(new { status = "Error", message = $"Transaction with ReceiptID {receiptId} not found." });
                    }

                    var query = @"
                SELECT [COMBINATION], [CUSTREQUEST], [FORMFORMAT], [FORMNUM],
                       TRY_CAST([INVOICEDATE] AS DATETIME) AS INVOICEDATE,
                       [INVOICENUM], [PURCHASERNAME], [RETAILTRANSACTIONTABLE], 
                       [RETAILTRANSRECIDGROUP], [SERIALNUM], [TAXCOMPANYADDRESS],
                       [TAXCOMPANYNAME], [TAXREGNUM], [TAXTRANSTXT], [TRANSTIME],
                       [DATAAREAID], [RECVERSION], [PARTITION], [RECID], [EMAIL],
                       [PHONE], [CUSTACCOUNT], [CANCEL], [CCCD], [MAQHNS]
                FROM [dbo].[VASRetailTransVATInformation]
                WHERE [RETAILTRANSACTIONTABLE] = @RecId";

                    var result = conn.QueryFirstOrDefault<VATInformation>(query, new { RecId = realRecid.Value });

                    if (result == null)
                        return NotFound(new { status = "Error", message = $"No VAT record found for RECID: {realRecid.Value}" });

                    return Ok(result);
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { status = "Error", message = ex.Message });
            }
        }
        [HttpGet("get-recid")]
        public IActionResult GetRecId([FromQuery] string receiptId)
        {
            if (string.IsNullOrEmpty(receiptId))
            {
                return BadRequest(new { status = "Error", message = "ReceiptID is required." });
            }

            try
            {
                using (var conn = new SqlConnection(_connString))
                {
                    conn.Open();

                    var query = @"SELECT TOP 1 RECID 
                          FROM [dbo].[RETAILTRANSACTIONTABLE]
                          WHERE RECEIPTID = @ReceiptId";

                    long? recId = conn.ExecuteScalar<long?>(query, new { ReceiptId = receiptId });

                    if (recId == null)
                    {
                        return NotFound(new { status = "Error", message = $"Transaction with ReceiptID {receiptId} not found." });
                    }

                    return Ok(recId.Value);
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { status = "Error", message = ex.Message });
            }

        }
        [HttpPut("addv2/{receiptId}")]
        public IActionResult UpdateVatInfo(string receiptId, [FromBody] ReceiptVATRequest data)
        {
            if (string.IsNullOrWhiteSpace(receiptId))
            {
                return BadRequest(new { status = "Error", message = "A valid ReceiptID must be provided in the URL." });
            }
            if (data == null)
            {
                return BadRequest(new { status = "Error", message = "Request body cannot be empty." });
            }

            try
            {
                using (var conn = new SqlConnection(_connString))
                {
                    conn.Open();

                    var recIdQuery = @"SELECT TOP 1 RECID 
                               FROM [dbo].[RETAILTRANSACTIONTABLE]
                               WHERE RECEIPTID = @ReceiptId";

                    var realRecid = conn.ExecuteScalar<long?>(recIdQuery, new { ReceiptId = receiptId });

                    if (realRecid == null)
                    {
                        return NotFound(new { status = "Error", message = $"Transaction with ReceiptID {receiptId} not found." });
                    }

                    var invoiceCheckQuery = @"SELECT INVOICENUM 
                                      FROM [dbo].[VASRetailTransVATInformation] 
                                      WHERE [RETAILTRANSACTIONTABLE] = @RecId";
                    var invoiceNum = conn.ExecuteScalar<string>(invoiceCheckQuery, new { RecId = realRecid.Value });

                    if (!string.IsNullOrEmpty(invoiceNum) && invoiceNum != "0")
                    {
                        return BadRequest(new { status = "Error", message = $"Einvoice already updated before = {invoiceNum}." });
                    }

                    var checkQuery = "SELECT COUNT(1) FROM [dbo].[VASRetailTransVATInformation] WHERE [RETAILTRANSACTIONTABLE] = @RecId";
                    var recordExists = conn.ExecuteScalar<bool>(checkQuery, new { RecId = realRecid.Value });

                    if (!recordExists)
                    {
                        return NotFound(new { status = "Error", message = $"Record with RECID {realRecid.Value} not found in VAT info." });
                    }

                    var updateQuery = @"
            UPDATE [dbo].[VASRetailTransVATInformation]
                SET 
                [TAXREGNUM] = @TAXREGNUM,
                [TAXCOMPANYNAME] = @TAXCOMPANYNAME,
                [TAXCOMPANYADDRESS] = @TAXCOMPANYADDRESS,
                [PURCHASERNAME] = @PURCHASERNAME,
                [EMAIL] = @EMAIL,
                [PHONE] = @PHONE,
                [CCCD] = @CCCD,
                [MAQHNS] = @MAQHNS,
                [CUSTREQUEST] = 1
                WHERE [RETAILTRANSACTIONTABLE] = @RECID;

            UPDATE [dbo].[VASRETAILTRANSVATINFORMATIONVIEW]
                SET 
                [TAXREGNUM] = @TAXREGNUM,
                [TAXCOMPANYNAME] = @TAXCOMPANYNAME,
                [TAXCOMPANYADDRESS] = @TAXCOMPANYADDRESS,
                [PURCHASERNAME] = @PURCHASERNAME,
                [CCCD] = @CCCD,
                [MAQHNS] = @MAQHNS,
                [CUSTREQUEST] = 1
                WHERE [RETAILTRANSACTIONTABLE] = @RECID;";
                    var parameters = new
                    {
                        RECID = realRecid.Value,
                        data.TAXREGNUM,
                        data.TAXCOMPANYNAME,
                        data.TAXCOMPANYADDRESS,
                        data.PURCHASERNAME,
                        data.EMAIL,
                        data.PHONE,
                        data.CCCD,
                        data.MAQHNS
                    };

                    var rowsAffected = conn.Execute(updateQuery, parameters);

                    return Ok(new { status = "Success", message = "VAT Information updated successfully." });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { status = "Error", message = ex.Message });
            }
        }

        [HttpPost("receipt")]
        public IActionResult CreateReceiptRecord([FromBody] ReceiptVATRequest request)
        {
            if (request == null)
            {
                return BadRequest();
            }

            try
            {
                using (var conn = new SqlConnection(_connString))
                {
                    DateTime? invoiceDate = null;
                    if (!string.IsNullOrEmpty(request.INVOICEDATE))
                    {
                        string[] formats = { "ddMMyyyy", "dd/MM/yyyy" };
                        if (DateTime.TryParseExact(request.INVOICEDATE, formats, CultureInfo.InvariantCulture,
                                                  DateTimeStyles.None, out DateTime parsedDate))
                        {
                            invoiceDate = parsedDate.Date;
                        }
                        else
                        {
                            return BadRequest();
                        }
                    }

                    conn.Open();

                    // check if RetailTransactionTable already exists
                    var existsQuery = @"SELECT COUNT(1) 
                                FROM [dbo].[ReceiptAPI] 
                                WHERE [RETAILTRANSACTIONTABLE] = @RetailTransactionTable";

                    var exists = conn.ExecuteScalar<int>(existsQuery, new { RetailTransactionTable = request.RETAILTRANSACTIONTABLE }) > 0;

                    if (exists)
                    {
                        return Conflict(new { status = "Error", message = "Transaction already exists" });
                    }

                    var nextRecIdQuery = @"SELECT ISNULL(MAX(RECID), 0) + 1 FROM [dbo].[ReceiptAPI]";
                    long nextRecId = conn.ExecuteScalar<long>(nextRecIdQuery);
                    int recVersion = 1;

                    var insertQuery = @"
            INSERT INTO [dbo].[ReceiptAPI]
            (
                RECID, RECVERSION,
                TAXREGNUM, TAXCOMPANYNAME, TAXCOMPANYADDRESS, INVOICEDATE, PURCHASERNAME,
                EMAIL, PHONE, CCCD, MAQHNS, DATAAREAID, RETAILTRANSACTIONTABLE
            )
            VALUES
            (
                @RECID, @RECVERSION,
                @TAXREGNUM, @TAXCOMPANYNAME, @TAXCOMPANYADDRESS, @INVOICEDATE, @PURCHASERNAME,
                @EMAIL, @PHONE, @CCCD, @MAQHNS, @DATAAREAID, @RETAILTRANSACTIONTABLE
            )";

                    var parameters = new
                    {
                        RECID = nextRecId,
                        RECVERSION = recVersion,
                        request.TAXREGNUM,
                        request.TAXCOMPANYNAME,
                        request.TAXCOMPANYADDRESS,
                        INVOICEDATE = invoiceDate,
                        request.PURCHASERNAME,
                        request.EMAIL,
                        request.PHONE,
                        request.CCCD,
                        request.MAQHNS,
                        request.DATAAREAID,
                        request.RETAILTRANSACTIONTABLE
                    };

                    conn.Execute(insertQuery, parameters);

                    return Ok();
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { status = "Error", message = ex.Message });
            }
        }

    }
}