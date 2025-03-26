namespace Gold_Billing_Web_App.Models
{
    // Represents a single metal transaction item
    public class MetalTransactionModel
    {
        public int? Id { get; set; }
        public string? BillNo { get; set; }
        public DateTime Date { get; set; }
        public int? AccountId { get; set; }
        public string? Narration { get; set; }
        public string Type { get; set; } = "Payment"; // Default to Payment
        public int? ItemId { get; set; }
        public decimal? GrossWeight { get; set; }
        public decimal? Tunch { get; set; }
        public decimal? Fine { get; set; }
        public ItemModel Item { get; set; }
        public AccountModel Account { get; set; }
    }

    // Represents the view model with a list of items
    public class MetalTransactionViewModel
    {
        public string? BillNo { get; set; }
        public DateTime Date { get; set; }
        public string? Narration { get; set; }
        public string Type { get; set; } = "Payment";
        public List<MetalTransactionModel> Items { get; set; } = new List<MetalTransactionModel>(); // Collection of items
        public ItemModel Item { get; set; }
    }
}