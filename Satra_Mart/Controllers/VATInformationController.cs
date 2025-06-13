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


        [HttpPut("addv2/{recid}")] 
        public IActionResult UpdateVatInfo(long recid, [FromBody] VatUpdateRequest data)
        {
            if (recid == 0)
            {
                return BadRequest(new { status = "Error", message = "A valid RECID must be provided in the URL." });
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

                    // 1. Check if the record exists before attempting to update it.
                    var checkQuery = "SELECT COUNT(1) FROM [AXR3_DEV].[dbo].[VASRetailTransVATInformation] WHERE [RECID] = @RecId";
                    var recordExists = conn.ExecuteScalar<bool>(checkQuery, new { RecId = recid });

                    if (!recordExists)
                    {
                        return NotFound(new { status = "Error", message = $"Record with RECID {recid} not found." });
                    }

                    // 2. If the record exists, proceed with the update.
                    var updateQuery = @"
                        UPDATE [AXR3_DEV].[dbo].[VASRetailTransVATInformation]
                        SET 
                            [TAXREGNUM] = @TAXREGNUM,
                            [TAXCOMPANYNAME] = @TAXCOMPANYNAME,
                            [TAXCOMPANYADDRESS] = @TAXCOMPANYADDRESS,
                            [PURCHASERNAME] = @PURCHASERNAME,
                            [EMAIL] = @EMAIL,
                            [PHONE] = @PHONE,
                            [CUSTREQUEST] = 1

                        WHERE 
                            [RECID] = @RECID";
                    var parameters = new
                    {
                        RECID = recid,
                        data.TAXREGNUM,
                        data.TAXCOMPANYNAME,
                        data.TAXCOMPANYADDRESS,
                        data.PURCHASERNAME,
                        data.EMAIL,
                        data.PHONE
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