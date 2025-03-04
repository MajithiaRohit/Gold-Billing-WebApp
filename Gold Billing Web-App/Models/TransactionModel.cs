using System.ComponentModel.DataAnnotations;

namespace Gold_Billing_Web_App.Models
{
    public class TransactionModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Transaction Type is required")]
        public string TransactionType { get; set; } = "";

        [Required(ErrorMessage = "Date is required")]
        [DataType(DataType.Date)]
        public DateTime Date { get; set; }

        public int? AccountId { get; set; }

        [Required(ErrorMessage = "Item is required")]
        [Range(1, int.MaxValue, ErrorMessage = "Please select a valid Item")]
        public int ItemId { get; set; }

        public string ItemName { get; set; } = "";

        [Range(0, int.MaxValue, ErrorMessage = "Pieces cannot be negative")]
        public int? Pc { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Gross Weight cannot be negative")]
        public decimal? Weight { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Less cannot be negative")]
        public decimal? Less { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Net Weight cannot be negative")]
        public decimal? NetWt { get; set; }

        [Range(0, 100, ErrorMessage = "Tunch must be between 0 and 100")]
        public decimal? Tunch { get; set; }

        [Range(0, 100, ErrorMessage = "Wastage must be between 0 and 100")]
        public decimal? Wastage { get; set; }

        [Range(0, 200, ErrorMessage = "Total Weight percentage must be between 0 and 200")]
        public decimal? TW { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Rate cannot be negative")]
        public decimal? Rate { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Fine cannot be negative")]
        public decimal? Fine { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Amount cannot be negative")]
        public decimal? Amount { get; set; }
    }
    

    public class TransactionViewModel
    {
        public string BillNo { get; set; } = "";
        public DateTime Date { get; set; }
        public string TransactionType { get; set; } = "";
        public string? Narration { get; set; }
        public List<TransactionModel> Items { get; set; } = new List<TransactionModel>();
    }
}
