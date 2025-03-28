namespace Gold_Billing_Web_App.Models
{
    public class MetalTransactionModel
    {
        public int? Id { get; set; }
        public string? BillNo { get; set; }
        public DateTime Date { get; set; }
        public int? AccountId { get; set; }
        public string? Narration { get; set; }
        public string Type { get; set; } = "Payment";
        public int? ItemId { get; set; }
        public decimal? GrossWeight { get; set; }
        public decimal? Tunch { get; set; }
        public decimal? Fine { get; set; }
        public ItemModel? Item { get; set; }
        public AccountModel? Account { get; set; }
        public int UserId { get; set; }
    }

    public class MetalTransactionViewModel
    {
        public int? SelectedAccountId { get; set; }
        public string? BillNo { get; set; }
        public DateTime Date { get; set; }
        public string? Narration { get; set; }
        public string Type { get; set; } = "Payment";
        public List<MetalTransactionModel> Items { get; set; } = new List<MetalTransactionModel>();
        public int UserId { get; set; }
    }
}