using Microsoft.AspNetCore.Mvc;
using System.Data.SqlClient;
using Dapper;
using Satra_Mart;

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
                                 FROM [AXR3_DEV].[dbo].[VASRetailTransVATInformation]";
                    var result = conn.Query<VATInformation>(query).AsList();
                    return Ok(result);
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        [HttpGet]
        [Route("details")]
        public IActionResult GetByRecId([FromQuery] long recid)
        {
            try
            {
                using (var conn = new SqlConnection(_connString))
                {
                    conn.Open();

                    var query = @"
                SELECT [COMBINATION], [CUSTREQUEST], [FORMFORMAT], [FORMNUM],
                       TRY_CAST([INVOICEDATE] AS DATETIME) AS INVOICEDATE,
                       [INVOICENUM], [PURCHASERNAME], [RETAILTRANSACTIONTABLE], 
                       [RETAILTRANSRECIDGROUP], [SERIALNUM], [TAXCOMPANYADDRESS],
                       [TAXCOMPANYNAME], [TAXREGNUM], [TAXTRANSTXT], [TRANSTIME],
                       [DATAAREAID], [RECVERSION], [PARTITION], [RECID], [EMAIL],
                       [PHONE], [CUSTACCOUNT], [CANCEL]
                FROM [AXR3_DEV].[dbo].[VASRetailTransVATInformation]
                WHERE [RECID] = @RecId";

                    var result = conn.QueryFirstOrDefault<VATInformation>(query, new { RecId = recid });

                    if (result == null)
                        return NotFound($"No VAT record found for RECID: {recid}");

                    return Ok(result);
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        [HttpPost("add")]
        public IActionResult AddVatInfo([FromBody] VATInformation data)
        {
            try
            {
                using (var conn = new SqlConnection(_connString))
                {
                    conn.Open();
                    var insertQuery = @"
                        INSERT INTO [AXR3_DEV].[dbo].[VASRetailTransVATInformation]
                        ([COMBINATION], [CUSTREQUEST], [FORMFORMAT], [FORMNUM], [INVOICEDATE],
                         [INVOICENUM], [PURCHASERNAME], [RETAILTRANSACTIONTABLE], [RETAILTRANSRECIDGROUP],
                         [SERIALNUM], [TAXCOMPANYADDRESS], [TAXCOMPANYNAME], [TAXREGNUM], [TAXTRANSTXT],
                         [TRANSTIME], [DATAAREAID], [RECVERSION], [PARTITION], [EMAIL],
                         [PHONE], [CUSTACCOUNT], [CANCEL], [RECID]) 
                        VALUES
                        (@COMBINATION, @CUSTREQUEST, @FORMFORMAT, @FORMNUM, @INVOICEDATE,
                         @INVOICENUM, @PURCHASERNAME, @RETAILTRANSACTIONTABLE, @RETAILTRANSRECIDGROUP,
                         @SERIALNUM, @TAXCOMPANYADDRESS, @TAXCOMPANYNAME, @TAXREGNUM, @TAXTRANSTXT,
                         @TRANSTIME, @DATAAREAID, @RECVERSION, @PARTITION, @EMAIL,
                         @PHONE, @CUSTACCOUNT, @CANCEL, @RECID)";

                    var rowsAffected = conn.Execute(insertQuery, data);

                    if (rowsAffected > 0)
                    {
                        return Ok(new { status = "Success", message = "VAT Information added successfully." });
                    }
                    else
                    {
                        return StatusCode(500, new { status = "Error", message = "Failed to insert record." });
                    }
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { status = "Error", message = ex.Message });
            }
        }


        [HttpPut("addv2/{receiptId}")]
        public IActionResult UpdateVatInfo(string receiptId, [FromBody] VatUpdateRequest data)
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

                    // 🔹 Step 1: Find RECID from RETAILTRANSACTIONTABLE
                    var recIdQuery = @"SELECT TOP 1 RECID 
                               FROM [AXR3_DEV].[dbo].[RETAILTRANSACTIONTABLE]
                               WHERE RECEIPTID = @ReceiptId";

                    var realRecid = conn.ExecuteScalar<long?>(recIdQuery, new { ReceiptId = receiptId });

                    if (realRecid == null)
                    {
                        return NotFound(new { status = "Error", message = $"Transaction with ReceiptID {receiptId} not found." });
                    }

                    // 🔹 Step 2: Check if record exists in VAT info table
                    var checkQuery = "SELECT COUNT(1) FROM [AXR3_DEV].[dbo].[VASRetailTransVATInformation] WHERE [RECID] = @RecId";
                    var recordExists = conn.ExecuteScalar<bool>(checkQuery, new { RecId = realRecid.Value });

                    if (!recordExists)
                    {
                        return NotFound(new { status = "Error", message = $"Record with RECID {realRecid.Value} not found in VAT info." });
                    }

                    // 🔹 Step 3: Update VAT info
                    var updateQuery = @"
                UPDATE [AXR3_DEV].[dbo].[VASRetailTransVATInformation]
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
                WHERE [RECID] = @RECID";

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

        //[HttpPost("add-with-identity")]
        //public IActionResult AddVatInfoWithIdentity([FromBody] VATInformation data)
        //{
        //    try
        //    {
        //        using (var conn = new SqlConnection(_connString))
        //        {
        //            conn.Open();
        //            var insertQuery = @"
        //                INSERT INTO [SatraMart].[dbo].[VASRetailTransVATInformation] 
        //                ([COMBINATION], [CUSTREQUEST], [FORMFORMAT], [FORMNUM], [INVOICEDATE],
        //                 [INVOICENUM], [PURCHASERNAME], [RETAILTRANSACTIONTABLE], [RETAILTRANSRECIDGROUP],
        //                 [SERIALNUM], [TAXCOMPANYADDRESS], [TAXCOMPANYNAME], [TAXREGNUM], [TAXTRANSTXT],
        //                 [TRANSTIME], [DATAAREAID], [RECVERSION], [PARTITION], [EMAIL],
        //                 [PHONE], [CUSTACCOUNT], [CANCEL])
        //                VALUES 
        //                (@COMBINATION, @CUSTREQUEST, @FORMFORMAT, @FORMNUM, @INVOICEDATE,
        //                 @INVOICENUM, @PURCHASERNAME, @RETAILTRANSACTIONTABLE, @RETAILTRANSRECIDGROUP,
        //                 @SERIALNUM, @TAXCOMPANYADDRESS, @TAXCOMPANYNAME, @TAXREGNUM, @TAXTRANSTXT,
        //                 @TRANSTIME, @DATAAREAID, @RECVERSION, @PARTITION, @EMAIL,
        //                 @PHONE, @CUSTACCOUNT, @CANCEL);
        //                SELECT CAST(SCOPE_IDENTITY() as bigint);";

        //            var newRecId = conn.QuerySingle<long>(insertQuery, data);

        //            return Ok(new
        //            {
        //                status = "Success",
        //                message = "VAT Information added successfully.",
        //                newRecId = newRecId
        //            });
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        return StatusCode(500, new { status = "Error", message = ex.Message });
        //    }
        //}
    }
}