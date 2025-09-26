namespace Satra_Mart
{
    public class ReceiptAPI
    {
        public int? Combination { get; set; }
        public int? CustRequest { get; set; }
        public string FormFormat { get; set; }
        public string FormNun { get; set; }
        public DateTime InvoiceDate { get; set; }
        public string InvoiceNum { get; set; }
        public string PurchaserName { get; set; }
        public long? RetailTransactionTable { get; set; }
        public long? RetailTransRecIdGroup { get; set; }
        public string SerialNum { get; set; }
        public string TaxCompanyAddress { get; set; }
        public string TaxCompanyName { get; set; }
        public string TaxRegNum { get; set; }
        public string TaxTransTxt { get; set; }
        public int? TransTime { get; set; }
        public string DataAreaId { get; set; }
        public int? RecVersion { get; set; }
        public long? Partition { get; set; }
        public long RecId { get; set; } 
        public string Email { get; set; }
        public string Phone { get; set; }
        public string CustAccount { get; set; }
        public int Cancel { get; set; }
        public string CCCD { get; set; }
        public string MaQhns { get; set; }
    }
}
