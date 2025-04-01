using System.ComponentModel.DataAnnotations;

namespace Gold_Billing_Web_App.Models.ViewModels
{
    public class RegisterViewModel
    {
        [Required]
        public string FullName { get; set; } = string.Empty;

        [Required]
        public string Username { get; set; } = string.Empty;

        [Required]
        public string Password { get; set; } = string.Empty;

        [Required]
        [Compare("Password")]
        public string ConfirmPassword { get; set; } = string.Empty;

        public string? CompanyName { get; set; }
        public string? CompanyAddress { get; set; }
        public string? Email { get; set; }
        public string? MobileNo { get; set; }
        public string? GstNumber { get; set; }
        public string? GodName1 { get; set; }
        public string? GodName2 { get; set; }
    }
}