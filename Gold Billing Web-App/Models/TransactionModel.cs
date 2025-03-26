using System.ComponentModel.DataAnnotations;

namespace Gold_Billing_Web_App.Models
{
    public class TransactionModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Bill Number is required.")]
        public string BillNo { get; set; } = "";

        [Required(ErrorMessage = "Date is required.")]
        public DateTime Date { get; set; }

        [Required(ErrorMessage = "Transaction Type is required.")]
        public string TransactionType { get; set; } = "";

        public int? AccountId { get; set; }

        [Required(ErrorMessage = "Item is required.")]
        public int? ItemId { get; set; }

        [Range(0, int.MaxValue, ErrorMessage = "Pieces must be a positive number.")]
        public int? Pc { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Gross Weight must be a positive number.")]
        public decimal? Weight { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Less must be a positive number.")]
        public decimal? Less { get; set; }

        public decimal? NetWt { get; set; }

        [Range(0, 100, ErrorMessage = "Tunch must be between 0 and 100.")]
        public decimal? Tunch { get; set; }

        [Range(0, 100, ErrorMessage = "Wastage must be between 0 and 100.")]
        public decimal? Wastage { get; set; }

        public decimal? TW { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Rate must be a positive number.")]
        public decimal? Rate { get; set; }

        public decimal? Fine { get; set; }
        public decimal? Amount { get; set; }

        public DateTime? LastUpdated { get; set; }

        public string? Narration { get; set; }

        public AccountModel Account { get; set; }
        public ItemModel Item { get; set; }
    }
}