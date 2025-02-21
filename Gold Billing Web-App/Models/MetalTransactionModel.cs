namespace Gold_Billing_Web_App.Models
{
    public class MetalTransactionModel
    {
        public string? BillNo { get; set; }
        public DateTime Date { get; set; }
        public int AccountId { get; set; }
        public string? Narration { get; set; }
        public required string Type { get; set; }
        public int ItemId { get; set; }
        public decimal GrossWeight { get; set; }
        public decimal Tunch { get; set; }
        public decimal Fine { get; set; }
    }
}
