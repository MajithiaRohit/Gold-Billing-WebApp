using System.ComponentModel.DataAnnotations;

namespace Gold_Billing_Web_App.Models.ViewModels
{
    public class LoginViewModel
    {
        [Required]
        public string Username { get; set; } = string.Empty;

        [Required]
        public string Password { get; set; } = string.Empty;
    }
}