namespace Gold_Billing_Web_App.Models
{
    public class AccountDropDownModel
    {
        public int Id { get; set; }
        public string AccountName { get; set; } = string.Empty;
        public string GroupName { get; set; } = string.Empty; // Matches GroupName from SP
        public AccountGroupModel? AccountGroup { get; set; }
    }
}
