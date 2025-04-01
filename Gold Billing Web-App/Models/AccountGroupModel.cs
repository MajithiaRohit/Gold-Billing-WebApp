using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;


namespace Gold_Billing_Web_App.Models
{
    public class AccountGroupModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Group name is required.")]
        [StringLength(510, ErrorMessage = "Group name cannot exceed 510 characters.")]
        public string GroupName { get; set; }

        public int UserId { get; set; }

        [NotMapped] // Exclude from model validation
        public UserAccountModel User { get; set; }
    }
}