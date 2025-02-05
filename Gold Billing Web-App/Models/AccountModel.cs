namespace Gold_Billing_Web_App.Models
{
    public class AccountModel
    {
        
        public string AccountName { get; set; }
        public string Group { get; set; }
        public int AccountNumber { get; set; }

        public string Address { get; set; }
        public string Location { get; set; }
        public string Occupation { get; set; }
        public string City { get; set; }
        public string State { get; set; }
        public string Country { get; set; }
        public string Pin { get; set; }
        public string Phone { get; set; }
        public string Mobile { get; set; }
        public string Email { get; set; }
        public string Website { get; set; }

        public DateTime DOB { get; set; }
        public DateTime Anniversary { get; set; }

        public string GSTIN { get; set; }
        public string PAN { get; set; }
        public string UID { get; set; }
        public string TAN { get; set; }
        public string CST { get; set; }

        public decimal GoldFine { get; set; }
        public decimal Amount { get; set; }
        public DateTime BalanceDate { get; set; }
        public decimal Discount { get; set; }
    }
}
