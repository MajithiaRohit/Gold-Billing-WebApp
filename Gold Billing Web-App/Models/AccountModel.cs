namespace Gold_Billing_Web_App.Models
{
    public class AccountModel
    {

        public int? AccountId { get; set; }
        public DateTime Date { get; set; }
        public string AccountName { get; set; }
        public int AccountGroupId { get; set; }
        public string Address { get; set; }
        public string City { get; set; }
        public string? Pincode { get; set; }
        public string MobileNo { get; set; }
        public string PhoneNo { get; set; }
        public string Email { get; set; }
        public decimal Fine { get; set; }
        public decimal Amount { get; set; }
    }
}
