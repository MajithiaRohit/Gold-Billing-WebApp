using System.ComponentModel.DataAnnotations;

namespace Gold_Billing_Web_App.Models
{
    public class AccountGroupModel
    {
        public int? Id { get; set; }

        [Required(ErrorMessage = "Group Name is required")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Group Name must be between 2 and 100 characters")]
        [RegularExpression(@"^[a-zA-Z0-9\s-&]+$", ErrorMessage = "Group Name can only contain letters, numbers, spaces, hyphens, and ampersands")]
        public string GroupName { get; set; } = "";
    }
}