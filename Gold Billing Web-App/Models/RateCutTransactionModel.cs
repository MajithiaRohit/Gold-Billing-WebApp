using System.ComponentModel.DataAnnotations;

namespace Gold_Billing_Web_App.Models
{
    public class RateCutTransactionModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Bill Number is required.")]
        [StringLength(20)]
        public string BillNo { get; set; } = string.Empty;

        [Required(ErrorMessage = "Date is required.")]
        public DateTime Date { get; set; }

        [Required(ErrorMessage = "The Account field is required.")]
        public int AccountId { get; set; }

        [Required(ErrorMessage = "Type is required.")]
        public string Type { get; set; } = string.Empty;

        [Required(ErrorMessage = "Weight is required.")]
        [Range(0.001, double.MaxValue)]
        public decimal Weight { get; set; }

        [Required(ErrorMessage = "Tunch is required.")]
        [Range(0.01, 100)]
        public decimal Tunch { get; set; }

        [Required(ErrorMessage = "Rate is required.")]
        [Range(0.01, double.MaxValue)]
        public decimal Rate { get; set; }

        public decimal Amount { get; set; }

        [StringLength(500)]
        public string? Narration { get; set; }

        public int UserId { get; set; }

        public AccountModel? Account { get; set; }

        public UserAccountModel? User { get; set; } // Nullable, optional
    }
}