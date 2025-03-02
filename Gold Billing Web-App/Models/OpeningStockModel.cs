using System.ComponentModel.DataAnnotations;

namespace Gold_Billing_Web_App.Models
{
    public class OpeningStockModel
    {
        public int Id { get; set; }

        [Required]
        [DataType(DataType.Date)]
        public DateTime Date { get; set; }

        [StringLength(50)]
        public string BillNo { get; set; }

        [StringLength(255)]
        public string? Narration { get; set; }

        [Required]
        public int ItemId { get; set; }

        [Required]
        public string ItemName { get; set; } // For display purposes

        public int? Pc { get; set; }

        public decimal? Weight { get; set; }

        public decimal? Less { get; set; }

        public decimal? NetWt { get; set; }

        public decimal Tunch { get; set; }

        public decimal Wastage { get; set; }
        public decimal TW { get; set; }

        public decimal? Rate { get; set; }

        public decimal? Fine { get; set; }

        public decimal? Amount { get; set; }
    }
}
