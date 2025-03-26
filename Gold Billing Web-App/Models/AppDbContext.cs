using Microsoft.EntityFrameworkCore;
using Gold_Billing_Web_App.Models;

namespace Gold_Billing_Web_App.Models
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<UserAccountModel> Users { get; set; }
        public DbSet<AccountModel> Accounts { get; set; }
        public DbSet<AmountTransactionModel> AmountTransactions { get; set; }
        public DbSet<AccountGroupModel> GroupAccounts { get; set; }
        public DbSet<ItemModel> Items { get; set; }
        public DbSet<ItemGroupModel> ItemGroups { get; set; }
        public DbSet<MetalTransactionModel> MetalTransactions { get; set; }
        public DbSet<OpeningStockModel> OpeningStocks { get; set; }
        public DbSet<PaymentModeDropDownModel> PaymentModes { get; set; }
        public DbSet<RateCutTransactionModel> RateCutTransactions { get; set; }
        public DbSet<TransactionModel> Transactions { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Account
            modelBuilder.Entity<AccountModel>(entity =>
            {
                entity.ToTable("Account");
                entity.HasKey(e => e.AccountId);
                entity.Property(e => e.Date).HasColumnType("date");
                entity.Property(e => e.AccountName).IsRequired().HasMaxLength(510);
                entity.Property(e => e.AccountGroupId).IsRequired();
                entity.Property(e => e.Address).HasMaxLength(510);
                entity.Property(e => e.City).HasMaxLength(200);
                entity.Property(e => e.Pincode).HasMaxLength(40);
                entity.Property(e => e.MobileNo).HasMaxLength(30);
                entity.Property(e => e.PhoneNo).HasMaxLength(30);
                entity.Property(e => e.Email).HasMaxLength(200);
                entity.Property(e => e.Fine).HasColumnType("decimal(18,2)");
                entity.Property(e => e.Amount).HasColumnType("decimal(18,2)");
                entity.HasOne(e => e.GroupAccount)
                      .WithMany()
                      .HasForeignKey(e => e.AccountGroupId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // AmountTransactions
            modelBuilder.Entity<AmountTransactionModel>(entity =>
            {
                entity.ToTable("AmountTransactions");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.BillNo).IsRequired().HasMaxLength(40);
                entity.Property(e => e.Date).HasColumnType("date");
                entity.Property(e => e.AccountId).IsRequired();
                entity.Property(e => e.Type).IsRequired().HasMaxLength(20);
                entity.Property(e => e.PaymentModeId).IsRequired();
                entity.Property(e => e.Amount).HasColumnType("decimal(18,2)");
                entity.Property(e => e.Narration).HasMaxLength(1000);
                entity.HasOne(e => e.Account)
                      .WithMany()
                      .HasForeignKey(e => e.AccountId)
                      .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(e => e.PaymentMode)
                      .WithMany()
                      .HasForeignKey(e => e.PaymentModeId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // GroupAccount
            modelBuilder.Entity<AccountGroupModel>(entity =>
            {
                entity.ToTable("GroupAccount");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.GroupName).IsRequired().HasMaxLength(510);
            });

            // Item
            modelBuilder.Entity<ItemModel>(entity =>
            {
                entity.ToTable("Item");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(510);
                entity.Property(e => e.ItemGroupId).IsRequired();
                entity.HasOne(e => e.ItemGroup)
                      .WithMany()
                      .HasForeignKey(e => e.ItemGroupId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // ItemGroup
            modelBuilder.Entity<ItemGroupModel>(entity =>
            {
                entity.ToTable("ItemGroup");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(510);
                entity.Property(e => e.Date).HasColumnType("date");
            });

            // MetalTransactions
            modelBuilder.Entity<MetalTransactionModel>(entity =>
            {
                entity.ToTable("MetalTransactions");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.BillNo).HasMaxLength(100);
                entity.Property(e => e.Date).HasColumnType("datetime");
                entity.Property(e => e.AccountId).IsRequired();
                entity.Property(e => e.Narration).HasMaxLength(1000);
                entity.Property(e => e.Type).IsRequired().HasMaxLength(100);
                entity.Property(e => e.ItemId).IsRequired();
                entity.Property(e => e.GrossWeight).HasColumnType("decimal(18,2)");
                entity.Property(e => e.Tunch).HasColumnType("decimal(18,2)");
                entity.Property(e => e.Fine).HasColumnType("decimal(18,2)");
                entity.HasOne(e => e.Item)
                      .WithMany()
                      .HasForeignKey(e => e.ItemId)
                      .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(e => e.Account)
                      .WithMany()
                      .HasForeignKey(e => e.AccountId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // OpeningStock
            modelBuilder.Entity<OpeningStockModel>(entity =>
            {
                entity.ToTable("OpeningStock");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.BillNo).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Date).HasColumnType("date");
                entity.Property(e => e.Narration).HasMaxLength(1000);
                entity.Property(e => e.ItemId).IsRequired();
                entity.Property(e => e.Pc).HasColumnType("int");
                entity.Property(e => e.Weight).HasColumnType("decimal(18,2)");
                entity.Property(e => e.Less).HasColumnType("decimal(18,2)");
                entity.Property(e => e.NetWt).HasColumnType("decimal(18,2)");
                entity.Property(e => e.Tunch).HasColumnType("decimal(18,2)");
                entity.Property(e => e.Wastage).HasColumnType("decimal(18,2)");
                entity.Property(e => e.TW).HasColumnType("decimal(18,2)");
                entity.Property(e => e.Rate).HasColumnType("decimal(18,2)");
                entity.Property(e => e.Fine).HasColumnType("decimal(18,2)");
                entity.Property(e => e.Amount).HasColumnType("decimal(18,2)");
                entity.Property(e => e.LastUpdated).HasColumnType("datetime");
                entity.HasOne(e => e.Item)
                      .WithMany()
                      .HasForeignKey(e => e.ItemId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // PaymentModes
            modelBuilder.Entity<PaymentModeDropDownModel>(entity =>
            {
                entity.ToTable("PaymentModes");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.ModeName).IsRequired().HasMaxLength(100);
            });

            // RateCutTransactions
            modelBuilder.Entity<RateCutTransactionModel>(entity =>
            {
                entity.ToTable("RateCutTransactions");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.BillNo).IsRequired().HasMaxLength(40);
                entity.Property(e => e.Date).HasColumnType("date");
                entity.Property(e => e.AccountId).IsRequired();
                entity.Property(e => e.Type).IsRequired().HasMaxLength(40);
                entity.Property(e => e.Weight).HasColumnType("decimal(18,2)");
                entity.Property(e => e.Tunch).HasColumnType("decimal(18,2)");
                entity.Property(e => e.Rate).HasColumnType("decimal(18,2)");
                entity.Property(e => e.Amount).HasColumnType("decimal(18,2)");
                entity.Property(e => e.Narration).HasMaxLength(1000);
                entity.HasOne(e => e.Account)
                      .WithMany()
                      .HasForeignKey(e => e.AccountId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // Transactions
            modelBuilder.Entity<TransactionModel>(entity =>
            {
                entity.ToTable("Transactions");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.BillNo).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Date).HasColumnType("date");
                entity.Property(e => e.TransactionType).IsRequired().HasMaxLength(40);
                entity.Property(e => e.AccountId).HasColumnType("int");
                entity.Property(e => e.ItemId).IsRequired();
                entity.Property(e => e.Pc).HasColumnType("int");
                entity.Property(e => e.Weight).HasColumnType("decimal(18,2)");
                entity.Property(e => e.Less).HasColumnType("decimal(18,2)");
                entity.Property(e => e.NetWt).HasColumnType("decimal(18,2)");
                entity.Property(e => e.Tunch).HasColumnType("decimal(18,2)");
                entity.Property(e => e.Wastage).HasColumnType("decimal(18,2)");
                entity.Property(e => e.TW).HasColumnType("decimal(18,2)");
                entity.Property(e => e.Rate).HasColumnType("decimal(18,2)");
                entity.Property(e => e.Fine).HasColumnType("decimal(18,2)");
                entity.Property(e => e.Amount).HasColumnType("decimal(18,2)");
                entity.Property(e => e.LastUpdated).HasColumnType("datetime");
                entity.Property(e => e.Narration).HasMaxLength(1000);
                entity.HasOne(e => e.Account)
                      .WithMany()
                      .HasForeignKey(e => e.AccountId)
                      .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(e => e.Item)
                      .WithMany()
                      .HasForeignKey(e => e.ItemId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // Users
            modelBuilder.Entity<UserAccountModel>(entity =>
            {
                entity.ToTable("Users");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Username).IsRequired().HasMaxLength(510);
                entity.Property(e => e.Password)
                      .IsRequired()
                      .HasMaxLength(60) // BCrypt hash length
                      .HasColumnType("nvarchar(60)") // Explicitly define column type
                      .HasComment("Stores BCrypt hashed password (60 characters)");
                entity.Property(e => e.FullName).HasMaxLength(510);
                entity.Property(e => e.CompanyName).HasMaxLength(510);
                entity.Property(e => e.CompanyAddress).HasMaxLength(510);
                entity.Property(e => e.Email).HasMaxLength(200);
                entity.Property(e => e.MobileNo).HasMaxLength(30);
                entity.Property(e => e.GstNumber).HasMaxLength(40);
                entity.Property(e => e.GodName1).HasMaxLength(510);
                entity.Property(e => e.GodName2).HasMaxLength(510);
            });
        }

        // Override SaveChanges to add validation for Password length
        public override int SaveChanges()
        {
            ValidatePasswordLength();
            return base.SaveChanges();
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            ValidatePasswordLength();
            return base.SaveChangesAsync(cancellationToken);
        }

        private void ValidatePasswordLength()
        {
            var userEntries = ChangeTracker.Entries<UserAccountModel>()
                .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified);

            foreach (var entry in userEntries)
            {
                var user = entry.Entity;
                if (!string.IsNullOrEmpty(user.Password) && user.Password.Length > 60)
                {
                    throw new InvalidOperationException($"Password hash for user {user.Username} exceeds 60 characters (length: {user.Password.Length}). BCrypt hash should be exactly 60 characters.");
                }
            }
        }
    }
}