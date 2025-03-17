namespace Gold_Billing_Web_App.Models
{
    public class RateCutTransactionModel
    {
        public int Id { get; set; }
        public string BillNo { get; set; } = string.Empty; // NVARCHAR(20)
        public DateTime Date { get; set; }                 // DATE
        public int AccountId { get; set; }                 // INT
        public string Type { get; set; } = string.Empty;   // "GoldPurchaseRate" or "GoldSaleRate"
        public decimal Weight { get; set; }                // DECIMAL(18,3) for Gross Weight
        public decimal Tunch { get; set; }                 // DECIMAL(18,2) for purity percentage
        public decimal Rate { get; set; }                  // DECIMAL(18,2) for gold rate
        public decimal Amount { get; set; }                // DECIMAL(18,2), readonly, calculated
        public string? Narration { get; set; }             // NVARCHAR(500), nullable
    }
}