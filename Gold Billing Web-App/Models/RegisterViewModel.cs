using System.ComponentModel.DataAnnotations;

namespace Gold_Billing_Web_App.Models
{
    public class RegisterViewModel
    {
        [Required]
        public string FullName { get; set; }

        [Required]
        public string Username { get; set; }

        public string? Password { get; set; } // Nullable, only used in Register

        [Compare("Password", ErrorMessage = "Passwords do not match")]
        public string? ConfirmPassword { get; set; } // Nullable, only used in Register

        public string? CompanyName { get; set; }

        public string? CompanyAddress { get; set; }

        [EmailAddress]
        public string? Email { get; set; }

        public string? MobileNo { get; set; }

        public string? GstNumber { get; set; }

        public string? GodName1 { get; set; }

        public string? GodName2 { get; set; }
    }
}