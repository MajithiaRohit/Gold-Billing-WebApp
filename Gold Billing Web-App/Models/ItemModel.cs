using System.ComponentModel.DataAnnotations;

namespace Gold_Billing_Web_App.Models
{
    public class ItemModel
    {
        [Required]
        public int? Id { get; set; }
        public required string Name { get; set; }
        public int ItemGroupId { get; set; }
    }
}
