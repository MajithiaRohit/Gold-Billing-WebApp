namespace Gold_Billing_Web_App.Models
{
    public class UserAccountModel
    {
        public int Id { get; set; }
        public string FullName { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string? CompanyName { get; set; }
        public string? CompanyAddress { get; set; }
        public string? Email { get; set; }
        public string? MobileNo { get; set; }
        public string? GstNumber { get; set; }
        public string? GodName1 { get; set; }
        public string? GodName2 { get; set; }
    }
}