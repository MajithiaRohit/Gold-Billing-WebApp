using System.ComponentModel.DataAnnotations;

namespace Gold_Billing_WebApp.Models
{
    public class VerifyUsernameViewModel
    {
        [Required]
        public string Username { get; set; } = string.Empty;

        public bool IsUsernameVerified { get; set; }

        public string? NewPassword { get; set; }

        public string? ConfirmPassword { get; set; }
    }
}