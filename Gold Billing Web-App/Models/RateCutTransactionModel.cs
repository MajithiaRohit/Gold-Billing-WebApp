using System.ComponentModel.DataAnnotations;

namespace Gold_Billing_Web_App.Models
{
    public class RateCutTransactionModel
    {
        public int Id { get; set; }
        [Required(ErrorMessage = "Bill Number is required.")]
        [StringLength(20, ErrorMessage = "Bill Number must not exceed 20 characters.")]
        public string BillNo { get; set; } = string.Empty;
        [Required(ErrorMessage = "Date is required.")]
        public DateTime Date { get; set; }
        [Required(ErrorMessage = "The Account field is required.")]
        public int AccountId { get; set; }
        [Required(ErrorMessage = "Type is required.")]
        public string Type { get; set; } = string.Empty; // "GoldPurchaseRate" or "GoldSaleRate"
        [Required(ErrorMessage = "Weight is required.")]
        [Range(0.001, double.MaxValue, ErrorMessage = "Weight must be greater than 0.")]
        public decimal Weight { get; set; }
        [Required(ErrorMessage = "Tunch is required.")]
        [Range(0.01, 100, ErrorMessage = "Tunch must be between 0.01 and 100.")]
        public decimal Tunch { get; set; }
        [Required(ErrorMessage = "Rate is required.")]
        [Range(0.01, double.MaxValue, ErrorMessage = "Rate must be greater than 0.")]
        public decimal Rate { get; set; }
        public decimal Amount { get; set; } // Calculated
        [StringLength(500, ErrorMessage = "Narration must not exceed 500 characters.")]
        public string? Narration { get; set; }
        public int UserId { get; set; } // For logical separation
        public AccountModel? Account { get; set; } // Nullable navigation property
    }
}