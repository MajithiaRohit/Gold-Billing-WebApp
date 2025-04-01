namespace Gold_Billing_Web_App.Models
{
    public class LedgerTransactionModel
    {
        public DateTime Date { get; set; }
        public string? Type { get; set; }
        public string? RefNo { get; set; }
        public string? Narration { get; set; }
        public int? Pc { get; set; }
        public decimal? GrWt { get; set; }
        public double? Less { get; set; }
        public decimal? NetWt { get; set; }
        public decimal? Tunch { get; set; }
        public decimal? Wstg { get; set; }
        public decimal? Rate { get; set; }
        public decimal? GoldFine { get; set; }
        public decimal? Amount { get; set; }
    }
}