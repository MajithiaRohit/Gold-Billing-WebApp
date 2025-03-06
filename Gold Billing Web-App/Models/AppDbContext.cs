using Microsoft.EntityFrameworkCore;

namespace Gold_Billing_Web_App.Models
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Transaction> Transactions { get; set; }
    }

    public class Transaction
    {
        public int Id { get; set; }
        public string TransactionType { get; set; } = "";
        public string BillNo { get; set; } = "";
        public DateTime Date { get; set; }
        public int? AccountId { get; set; }
        public int ItemId { get; set; }
        public int? Pc { get; set; }
        public decimal? Weight { get; set; }
        public decimal? Less { get; set; }
        public decimal? NetWt { get; set; }
        public decimal? Tunch { get; set; }
        public decimal? Wastage { get; set; }
        public decimal? TW { get; set; }
        public decimal? Rate { get; set; }
        public decimal? Fine { get; set; }
        public decimal? Amount { get; set; }
        public DateTime? LastUpdated { get; set; }
    }
}