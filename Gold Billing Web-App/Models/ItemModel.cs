using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Gold_Billing_Web_App.Models
{
    public class ItemModel
    {
        public int? Id { get; set; }

        [Required(ErrorMessage = "Item Name is required")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Item Name must be between 2 and 100 characters")]
        [RegularExpression(@"^[a-zA-Z0-9\s-&]+$", ErrorMessage = "Item Name can only contain letters, numbers, spaces, hyphens, and ampersands")]
        public string? Name { get; set; }

        [Required(ErrorMessage = "Item Group is required")]
        [Range(1, int.MaxValue, ErrorMessage = "Please select a valid Item Group")]
        public int ItemGroupId { get; set; }

        [NotMapped]
        public ItemGroupModel? ItemGroup { get; set; }

        public int UserId { get; set; }

        [NotMapped]
        public UserAccountModel User { get; set; }
    }
}