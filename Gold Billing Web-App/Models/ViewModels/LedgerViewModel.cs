namespace Gold_Billing_Web_App.Models
{
    public class LedgerViewModel
    {
        public int UserId { get; set; } // Changed from Guid to int
        public int? SelectedAccountId { get; set; }
        public List<AccountDropDownModel> Accounts { get; set; } = new List<AccountDropDownModel>();
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public List<LedgerTransactionModel> Transactions { get; set; } = new List<LedgerTransactionModel>();
    }
}