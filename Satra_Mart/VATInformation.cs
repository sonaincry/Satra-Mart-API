using System;

namespace Satra_Mart
{
    public class VATInformation
    {
        public int COMBINATION { get; set; }
        public int CUSTREQUEST { get; set; }

        public string FORMFORMAT { get; set; }
        public string FORMNUM { get; set; }

        public DateTime INVOICEDATE { get; set; }

        public string INVOICENUM { get; set; }

        public string PURCHASERNAME { get; set; }

        public long RETAILTRANSACTIONTABLE { get; set; }
        public long RETAILTRANSRECIDGROUP { get; set; }

        public string SERIALNUM { get; set; }
        public string TAXCOMPANYADDRESS { get; set; }
        public string TAXCOMPANYNAME { get; set; }
        public string TAXREGNUM { get; set; }
        public string TAXTRANSTXT { get; set; }
        public int TRANSTIME { get; set; }
        public string DATAAREAID { get; set; }
        public int RECVERSION { get; set; }
        public long PARTITION { get; set; }
        public long RECID { get; set; }
        public string EMAIL { get; set; }
        public string PHONE { get; set; }
        public string CCCD { get; set; }
        public string MAQHNS { get; set; }
        public string CUSTACCOUNT { get; set; }
        public int CANCEL { get; set; }
    }
}
