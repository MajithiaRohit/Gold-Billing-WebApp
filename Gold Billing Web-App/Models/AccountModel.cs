using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;


namespace Gold_Billing_Web_App.Models
{
    public class AccountModel
    {
        public int AccountId { get; set; }

        [Required(ErrorMessage = "Account name is required.")]
        public string AccountName { get; set; }

        [Required(ErrorMessage = "Account group is required.")]
        public int AccountGroupId { get; set; }

        public string Address { get; set; } = "N/A"; // Default value
        public string City { get; set; } = "N/A";
        public string Pincode { get; set; } = "N/A";
        public string MobileNo { get; set; } = "N/A";
        public string PhoneNo { get; set; } = "N/A";

        [EmailAddress(ErrorMessage = "Invalid email address.")]
        public string Email { get; set; } = "N/A";

        public decimal Fine { get; set; } 
        public decimal Amount { get; set; }

        [Required(ErrorMessage = "Opening date is required.")]
        [DataType(DataType.Date)]
        [CustomValidation(typeof(AccountModel), nameof(ValidateOpeningDate))]
        public DateTime OpeningDate { get; set; }

        public DateTime? LastUpdated { get; set; }
        public int UserId { get; set; }
        [NotMapped]
        public AccountGroupModel GroupAccount { get; set; }
        [NotMapped]
        public UserAccountModel User { get; set; } // Added

        public static ValidationResult ValidateOpeningDate(DateTime openingDate, ValidationContext context)
        {
            if (openingDate > DateTime.Now)
            {
                return new ValidationResult("Opening date cannot be in the future.");
            }
            return ValidationResult.Success;
        }
    }
}