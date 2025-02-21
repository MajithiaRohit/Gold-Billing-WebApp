namespace Gold_Billing_Web_App.Models
{
    public class AmountTransactionModel
    {
        public string? BillNo { get; set; }
        public DateTime Date { get; set; }
        public int AccountId { get; set; }
        public required string Type { get; set; }
        public int PaymentModeId { get; set; }
        public decimal Amount { get; set; }
        public string? Narration { get; set; }
    }
}
