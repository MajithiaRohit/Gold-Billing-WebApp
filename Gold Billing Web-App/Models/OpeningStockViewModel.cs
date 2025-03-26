using System.ComponentModel.DataAnnotations;

namespace Gold_Billing_Web_App.Models
{
    public class OpeningStockViewModel
    {
        [Required(ErrorMessage = "Bill Number is required")]
        public string BillNo { get; set; } = "";

        [Required(ErrorMessage = "Date is required")]
        [DataType(DataType.Date)]
        public DateTime Date { get; set; }

        public string? Narration { get; set; }

        [Required(ErrorMessage = "At least one item is required")]
        [MinLength(1, ErrorMessage = "At least one item is required")]
        public List<OpeningStockModel> Items { get; set; } = new List<OpeningStockModel>();

        // Optional: Include this if you need it in the UI
        public ItemModel? Item { get; set; }
    }
}