namespace Gold_Billing_Web_App.Models
{
    public class AmountTransactionModel
    {
        public int Id { get; set; }                        // Added to match table's Id column
        public string BillNo { get; set; } = string.Empty; // NVARCHAR(20), not null
        public DateTime Date { get; set; }                 // DATE, not null
        public int AccountId { get; set; }                 // INT, not null
        public string Type { get; set; } = string.Empty;   // NVARCHAR(10), not null
        public int PaymentModeId { get; set; }             // INT, not null
        public decimal Amount { get; set; }                // DECIMAL(18,2), not null
        public string? Narration { get; set; }             // NVARCHAR(500), nullable
    }
}