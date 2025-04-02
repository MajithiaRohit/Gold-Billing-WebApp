namespace Gold_Billing_Web_App.Models.ViewModels
{
    public class StockDetailsViewModel
    {
        public int ItemId { get; set; }
        public string ItemName { get; set; }
        public List<OpeningStockModel> StockItems { get; set; }
        public int TotalPc { get; set; }
        public decimal TotalWeight { get; set; }
        public decimal TotalNetWt { get; set; }
        public decimal TotalFine { get; set; }
        public decimal TotalAmount { get; set; }
    }
}