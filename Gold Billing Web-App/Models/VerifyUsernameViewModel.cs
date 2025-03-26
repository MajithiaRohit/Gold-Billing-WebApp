using System.ComponentModel.DataAnnotations;

namespace Gold_Billing_WebApp.Models
{
    public class VerifyUsernameViewModel
    {
        [Required(ErrorMessage = "Username is required.")]
        public string Username { get; set; } = string.Empty;

        public bool IsUsernameVerified { get; set; }

        public string NewPassword { get; set; } = string.Empty;

        public string ConfirmPassword { get; set; } = string.Empty;
    }
}