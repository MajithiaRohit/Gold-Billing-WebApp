namespace Gold_Billing_Web_App.Models
{
    public class RateCutTransaction
    {
        public string? BillNo { get; set; }
        public int AccountId { get; set; }
        public DateTime Date { get; set; }
        public string? Narration { get; set; }
        public required string Type { get; set; }
        public decimal Weight { get; set; }
        public decimal Tunch { get; set; }
        public decimal Rate { get; set; }
        public decimal Amount { get; set; }
    }
}
