namespace Satra_Mart
{
    public class ReceiptVATRequest
    {
        public string TAXREGNUM { get; set; }
        public string TAXCOMPANYNAME { get; set; }
        public string TAXCOMPANYADDRESS { get; set; }   
        public string INVOICEDATE { get; set; }
        public string PURCHASERNAME { get; set; }
        public string EMAIL { get; set; }
        public string PHONE { get; set; }
        public string CCCD { get; set; }
        public string MAQHNS { get; set; }

        public string DATAAREAID { get; set; }
        public long RETAILTRANSACTIONTABLE { get; set; }
        public string RETAILRECEIPTID { get; set; }
        public string RETAILSTOREID { get; set; }


    }
}
