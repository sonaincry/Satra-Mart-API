using Dapper;
using Microsoft.AspNetCore.Mvc;
using Satra_Mart;
using System;
using System.Collections.Concurrent;
using System.Data.SqlClient;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Satra_Mart.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class VATInformationController : ControllerBase
    {
        private readonly string _connString;

        private static ConcurrentDictionary<string, string> _storeNameCache = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static bool _storeCacheLoaded = false;
        private static readonly object _cacheLock = new object();

        public VATInformationController(IConfiguration configuration)
        {
            _connString = configuration.GetConnectionString("DefaultConnection");

            if (!_storeCacheLoaded)
            {
                lock (_cacheLock)
                {
                    if (!_storeCacheLoaded)
                    {
                        LoadStoreNameCache();
                        _storeCacheLoaded = true;
                    }
                }
            }
        }

        private void LoadStoreNameCache()
        {
            try
            {
                using (var conn = new SqlConnection(_connString))
                {
                    conn.Open();
                    var query = @"
SELECT a.STORENUMBER, b.NAME
FROM RETAILCHANNELTABLE a
JOIN (SELECT recid FROM DIRPARTYTABLE WHERE INSTANCERELATIONTYPE = 2377) AS dt ON dt.RECID = a.OMOPERATINGUNITID
JOIN DIRPARTYTABLE b ON b.RECID = dt.RECID
WHERE a.partition = 5637144576";

                    var rows = conn.Query(query);
                    foreach (var row in rows)
                    {
                        string storeNumber = row.STORENUMBER?.ToString();
                        string storeName = row.NAME?.ToString();
                        if (!string.IsNullOrEmpty(storeNumber))
                            _storeNameCache[storeNumber] = storeName ?? "Unknown Store";
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to load store name cache: {ex.Message}");
            }
        }

        [HttpPost("refresh-store-cache")]
        public IActionResult RefreshStoreCache()
        {
            lock (_cacheLock)
            {
                _storeNameCache.Clear();
                _storeCacheLoaded = false;
                LoadStoreNameCache();
                _storeCacheLoaded = true;
            }
            return Ok(new { message = $"Store cache refreshed. {_storeNameCache.Count} stores loaded." });
        }

        [HttpGet("transaction-info/{receiptId}")]
        public IActionResult GetTransactionInfo(
            string receiptId,
            [FromQuery] string date)
        {
            if (string.IsNullOrWhiteSpace(receiptId))
            {
                return BadRequest(new
                {
                    status = "Error",
                    message = "ReceiptId is required"
                });
            }

            try
            {
                using (var conn = new SqlConnection(_connString))
                {
                    conn.Open();

                    // Parse date from query param (format: yyyy-MM-dd)
                    DateTime? transDate = null;
                    if (!string.IsNullOrWhiteSpace(date) &&
                        DateTime.TryParseExact(date, "yyyy-MM-dd",
                            CultureInfo.InvariantCulture,
                            DateTimeStyles.None, out var parsedDate))
                    {
                        transDate = parsedDate;
                    }

                    var query = @"
SELECT TOP 1
    RECEIPTID,
    PAYMENTAMOUNT,
    STORE
FROM [dbo].[RETAILTRANSACTIONTABLE]
WHERE RECEIPTID = @ReceiptId
  AND PARTITION = 5637144576
  AND (@TransDate IS NULL OR TRANSDATE = @TransDate)";

                    var result = conn.QueryFirstOrDefault(query, new
                    {
                        ReceiptId = receiptId,
                        TransDate = transDate
                    });

                    if (result == null)
                    {
                        return NotFound(new
                        {
                            status = "Error",
                            message = $"Transaction not found for ReceiptId {receiptId}"
                        });
                    }

                    string storeNumber = result.STORE?.ToString();
                    string storeName = "Unknown Store";
                    if (!string.IsNullOrEmpty(storeNumber))
                        _storeNameCache.TryGetValue(storeNumber, out storeName);

                    return Ok(new
                    {
                        receiptId = result.RECEIPTID,
                        paymentAmount = result.PAYMENTAMOUNT,
                        storeName = storeName ?? "Unknown Store"
                    });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    status = "Error",
                    message = ex.Message
                });
            }
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

        [HttpGet("details")]
        public IActionResult GetDetailsSecure(
            [FromQuery] string receiptid,
            [FromQuery] string dataareaid,
            [FromQuery] string storeno,
            [FromQuery] string date,
            [FromQuery] string sign,
            [FromServices] IConfiguration config)
        {
            if (!ValidateSignature(receiptid, dataareaid, storeno, date, sign, config))
                return Unauthorized(new { message = "Invalid signature" });

            using (var conn = new SqlConnection(_connString))
            {
                conn.Open();

                var recIdQuery = @"SELECT TOP 1 RECID 
                           FROM RETAILTRANSACTIONTABLE
                           WHERE RECEIPTID = @ReceiptId";

                var recId = conn.ExecuteScalar<long?>(recIdQuery, new { ReceiptId = receiptid });

                if (recId == null)
                    return NotFound("Receipt not found");

                var query = @"
            SELECT *
            FROM VASRetailTransVATInformation
            WHERE RETAILTRANSACTIONTABLE = @RecId";

                var result = conn.QueryFirstOrDefault<VATInformation>(query, new { RecId = recId });

                return Ok(result);
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

        [HttpPost("receipt")]
        public IActionResult CreateReceiptRecord(
            [FromQuery] string receiptid,
            [FromQuery] string dataareaid,
            [FromQuery] string storeno,
            [FromQuery] string date,
            [FromQuery] string sign,
            [FromBody] ReceiptVATRequest request,
            [FromServices] IConfiguration config)
        {
            if (request == null)
                return BadRequest("Request body missing");

            if (!ValidateSignature(receiptid, dataareaid, storeno, date, sign, config))
                return Unauthorized("Invalid signature");

            if (!string.Equals(receiptid, request.RETAILRECEIPTID, StringComparison.OrdinalIgnoreCase))
                return BadRequest("ReceiptId mismatch");

            using (var conn = new SqlConnection(_connString))
            {
                conn.Open();

                var existsQuery = @"
                    SELECT COUNT(1)
                    FROM ReceiptAPI
                    WHERE RETAILRECEIPTID = @ReceiptId";

                bool exists = conn.ExecuteScalar<int>(existsQuery,
                    new { ReceiptId = receiptid }) > 0;

                if (exists)
                    return Conflict("Receipt already created");

                var insertQuery = @"
                    INSERT INTO ReceiptAPI
                    (RECID, RECVERSION, TAXREGNUM, TAXCOMPANYNAME,
                     TAXCOMPANYADDRESS, INVOICEDATE, PURCHASERNAME,
                     EMAIL, PHONE, CCCD, MAQHNS, DATAAREAID,
                     TRANSDATE, RETAILRECEIPTID, RETAILSTOREID, QRREQUEST)
                    VALUES
                    ((SELECT ISNULL(MAX(RECID),0)+1 FROM ReceiptAPI),
                     1, @TAXREGNUM, @TAXCOMPANYNAME, @TAXCOMPANYADDRESS,
                     @INVOICEDATE, @PURCHASERNAME,
                     @EMAIL, @PHONE, @CCCD, @MAQHNS, @DATAAREAID,
                     @INVOICEDATE, @RETAILRECEIPTID, @RETAILSTOREID, 1)";

                conn.Execute(insertQuery, request);
            }

            return Ok("Receipt saved successfully");
        }

        public static class HmacHelper
        {
            public static string ComputeSignature(string secretKey, string data, bool keyIsBase64, bool returnBase64)
            {
                byte[] keyBytes = keyIsBase64
                    ? Convert.FromBase64String(secretKey)
                    : Encoding.UTF8.GetBytes(secretKey);

                var dataBytes = Encoding.UTF8.GetBytes(data);

                using (var hmac = new HMACSHA256(keyBytes))
                {
                    var hash = hmac.ComputeHash(dataBytes);

                    return returnBase64
                        ? Convert.ToBase64String(hash)
                        : BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                }
            }

            public static string ComputeSignatureAXFormat(string secretKey, string data)
            {
                var keyBytes = Encoding.UTF8.GetBytes(secretKey);
                var dataBytes = Encoding.UTF8.GetBytes(data);

                using (var hmac = new HMACSHA256(keyBytes))
                {
                    var hash = hmac.ComputeHash(dataBytes);
                    return Convert.ToBase64String(hash);
                }
            }
        }

        private bool ValidateSignature(
            string receiptid,
            string dataareaid,
            string storeno,
            string date,
            string sign,
            IConfiguration config)
        {
            string secretKey = config["AppSettings:HmacSecret"];
            string rawData = $"{receiptid}{dataareaid}{storeno}{date}".ToLower();

            string expected = HmacHelper.ComputeSignatureAXFormat(secretKey, rawData);
            return string.Equals(sign, expected, StringComparison.OrdinalIgnoreCase);
        }
    }
}