using System.ComponentModel.DataAnnotations;

namespace Gold_Billing_Web_App.Models
{
    public class TransactionModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Transaction Type is required.")]
        public string TransactionType { get; set; } = "";

        [Display(Name = "Bill Number")]
        public string BillNo { get; set; } = ""; // Remove [Required]

        [Required(ErrorMessage = "Date is required.")]
        public DateTime Date { get; set; }

        public string? Narration { get; set; }

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
        public DateTime LastUpdated { get; set; }
        public int UserId { get; set; }

        public AccountModel? Account { get; set; }
        public ItemModel? Item { get; set; }
    }
}