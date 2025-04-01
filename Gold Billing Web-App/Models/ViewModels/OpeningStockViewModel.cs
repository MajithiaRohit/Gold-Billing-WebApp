using System.ComponentModel.DataAnnotations;

namespace Gold_Billing_Web_App.Models.ViewModels
{
    public class OpeningStockViewModel
    {
        public string BillNo { get; set; } = ""; // Remove [Required]

        [Required(ErrorMessage = "Date is required")]
        [DataType(DataType.Date)]
        public DateTime Date { get; set; }

        public string? Narration { get; set; }

        public int UserId { get; set; } // Remove [Required]

        [MinLength(1, ErrorMessage = "At least one item is required")]
        public List<OpeningStockModel> Items { get; set; } = new List<OpeningStockModel>();
    }
}