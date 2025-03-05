using Microsoft.EntityFrameworkCore;

namespace Gold_Billing_Web_App.Models
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<TransactionModel> Transactions { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TransactionModel>().ToTable("Transactions"); // Matches your query
        }
    }
}