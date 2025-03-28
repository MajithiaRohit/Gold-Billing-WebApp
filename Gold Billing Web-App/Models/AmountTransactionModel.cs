using System.ComponentModel.DataAnnotations;

namespace Gold_Billing_Web_App.Models
{
    public class AmountTransactionModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Bill Number is required.")]
        [StringLength(40, ErrorMessage = "Bill Number must not exceed 40 characters.")]
        public string BillNo { get; set; } = string.Empty;

        [Required(ErrorMessage = "Date is required.")]
        public DateTime Date { get; set; }

        [Required(ErrorMessage = "The Account field is required.")]
        public int AccountId { get; set; }

        [Required(ErrorMessage = "Type is required.")]
        [StringLength(20, ErrorMessage = "Type must not exceed 20 characters.")]
        public string Type { get; set; } = string.Empty;

        [Required(ErrorMessage = "The Payment Mode field is required.")]
        public int PaymentModeId { get; set; }

        [Required(ErrorMessage = "Amount is required.")]
        [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than 0.")]
        public decimal Amount { get; set; }

        [StringLength(1000, ErrorMessage = "Narration must not exceed 1000 characters.")]
        public string? Narration { get; set; }

        public int UserId { get; set; }

        public AccountModel? Account { get; set; } // Made nullable
        public PaymentModeDropDownModel? PaymentMode { get; set; } // Made nullable
    }
}