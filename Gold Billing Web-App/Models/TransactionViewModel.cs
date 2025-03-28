using System.ComponentModel.DataAnnotations;

namespace Gold_Billing_Web_App.Models
{
    public class TransactionViewModel
    {
        [Required]
        [Display(Name = "Bill Number")]
        public string? BillNo { get; set; }

        [Required(ErrorMessage = "Date is required.")]
        public DateTime Date { get; set; }

        public string? Narration { get; set; } // Optional, no validation

        [Required(ErrorMessage = "Transaction Type is required.")]
        public string TransactionType { get; set; } = "";

        [Required(ErrorMessage = "At least one item is required.")]
        [MinLength(1, ErrorMessage = "At least one item is required.")]
        public List<TransactionModel> Items { get; set; } = new List<TransactionModel>();

        // Optional: Include these if you need them in the UI
        public AccountModel? Account { get; set; }
        public ItemModel? Item { get; set; }

        public int UserId { get; set; } // Add UserId to the view model
    }
}