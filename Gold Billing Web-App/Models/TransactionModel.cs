namespace Gold_Billing_Web_App.Models
{
    public class TransactionModel
    {
        public int Id { get; set; }
        public string TransactionType { get; set; } = "";
        public int? AccountId { get; set; }
        public int? ItemId { get; set; }
        public int? Pc { get; set; }
        public decimal? Weight { get; set; }
        public decimal? Less { get; set; }
        public decimal? NetWt { get; set; }
        public decimal? Tunch { get; set; }
        public decimal? Wastage { get; set; }
        public decimal? TW { get; set; }
        public decimal? Rate { get; set; }
        public decimal? Fine { get; set; }
        public decimal? Amount { get; set; }
    }

    public class TransactionViewModel
    {
        public string BillNo { get; set; } = "";
        public DateTime Date { get; set; }
        public string? Narration { get; set; } // Included Narration
        public string TransactionType { get; set; } = "";
        public List<TransactionModel> Items { get; set; } = new List<TransactionModel>();
    }
}