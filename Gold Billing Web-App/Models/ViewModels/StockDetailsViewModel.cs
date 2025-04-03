namespace Gold_Billing_Web_App.Models.ViewModels
{
    public class StockEntry
    {
        public string BillNo { get; set; }
        public string Type { get; set; } // "Opening", "Purchase", "Sale", etc.
        public string Sign { get; set; } // "+" or "-"
        public string ItemName { get; set; }
        public int? Pc { get; set; }
        public decimal? Weight { get; set; }
        public decimal? Less { get; set; }
        public decimal? NetWt { get; set; }
        public decimal? Tunch { get; set; }
        public decimal? Wastage { get; set; }
        public decimal? Fine { get; set; }
        public decimal? Amount { get; set; }
        public DateTime Date { get; set; }
    }

    public class StockDetailsViewModel
    {
        public int ItemId { get; set; }
        public string ItemName { get; set; }
        public List<StockEntry> Entries { get; set; } // Replaced StockItems and Transactions
        public int TotalPc { get; set; }
        public decimal TotalWeight { get; set; }
        public decimal TotalNetWt { get; set; }
        public decimal TotalFine { get; set; }
        public decimal TotalAmount { get; set; }
    }
}