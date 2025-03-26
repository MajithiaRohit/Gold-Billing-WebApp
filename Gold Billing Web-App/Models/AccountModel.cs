using System.ComponentModel.DataAnnotations;

namespace Gold_Billing_Web_App.Models
{
    public class AccountModel
    {
        public AccountGroupModel GroupAccount { get; set; }
        public int? AccountId { get; set; }

        public DateTime Date { get; set; }

        [Required(ErrorMessage = "Account Name is required")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Account Name must be between 2 and 100 characters")]
        public string AccountName { get; set; } = "";

        [Required(ErrorMessage = "Account Group is required")]
        [Range(1, int.MaxValue, ErrorMessage = "Please select a valid Account Group")]
        public int AccountGroupId { get; set; }

        [StringLength(500, ErrorMessage = "Address cannot exceed 500 characters")]
        public string Address { get; set; } = "";

        [StringLength(50, ErrorMessage = "City cannot exceed 50 characters")]
        public string City { get; set; } = "";

        [StringLength(10, ErrorMessage = "Pincode cannot exceed 10 characters")]
        [RegularExpression(@"^\d{6}$", ErrorMessage = "Pincode must be a 6-digit number")]
        public string? Pincode { get; set; }

        [Required(ErrorMessage = "Mobile Number is required")]
        [RegularExpression(@"^\d{10}$", ErrorMessage = "Mobile Number must be a 10-digit number")]
        public string MobileNo { get; set; } = "";

        [StringLength(15, ErrorMessage = "Phone Number cannot exceed 15 characters")]
        [RegularExpression(@"^\d+$", ErrorMessage = "Phone Number must contain only digits")]
        public string PhoneNo { get; set; } = "";

        [EmailAddress(ErrorMessage = "Invalid Email Address")]
        public string Email { get; set; } = "";

        [Range(0, double.MaxValue, ErrorMessage = "Fine cannot be negative")]
        public decimal Fine { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Amount cannot be negative")]
        public decimal Amount { get; set; }
    }
 
}