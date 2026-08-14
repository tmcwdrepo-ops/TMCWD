using Microsoft.EntityFrameworkCore;
using TMCWD.Data.Entities;

namespace TMCWD.Data.Context
{
    public class UserDbContext : DbContext
    {
        #region Fields

        string connectionString = "server=localhost;port=3306;database=tmcwd;user=root;password=password123;";

        public DbSet<User> Users { get; set; }

        public DbSet<InspectionType> InspectionTypes { get; set; }

        public DbSet<Customer> Customers { get; set; }

        public DbSet<InspectionTypeDetail> InspectonTypeDetails { get; set; }

        public DbSet<Account> Accounts { get; set; }

        public DbSet<Request> Requests { get; set;  }

        public DbSet<RequestDetail> RequestDetails { get; set; }

        public DbSet<InspectionReport> InspectionReports { get; set; }

        public DbSet<Recommendation> Recommendations { get; set; }

        public DbSet<Inventory> Inventories { get; set; }

        public DbSet<Material> Materials { get; set; }

        public DbSet<OtherFeeType> OtherFeeTypes { get; set;  }

        public DbSet<Workflow> Workflows { get; set; }

        public DbSet<JobOrder> JobOrders { get; set; }

        public DbSet<Finding> Findings { get; set; }

        public DbSet<ApprovalHistory> ApprovalHistories { get; set; }

        public DbSet<RequestFile> Files { get; set; }

        public DbSet<Billing> Billings { get; set; }

        public DbSet<ReadingSheet> ReadingSheets { get; set; }

        public DbSet<Penalty> Penalties { get; set; }

        public DbSet<ChargeType> ChargeTypes { get; set; }

        public DbSet<OtherCharge> OtherCharges { get; set; }

        public DbSet<BillingAdjustment> BillingAdjustments { get; set; }

        public DbSet<PaymentCheck> PaymentChecks { get; set; }

        public DbSet<AdvancePayment> AdvancePayments { get; set; }

        public DbSet<WebHook> WebHooks { get; set; }

        public DbSet<Payment> Payments { get; set; }

        public DbSet<EWalletTransaction> EWalletTransactions { get; set; }

        public DbSet<TMCWD.Data.Entities.Endpoint> Endpoints { get; set; }

        public DbSet<Reading> Readings { get; set; }

        public DbSet<Tariff> Tariffs { get; set; }

        public DbSet<WaterRate> WaterRates { get; set; }

        public DbSet<ZoneBook> ZoneBooks { get; set; }

        public DbSet<ReadingSheetTemplate> ReadingSheetTemplates { get; set; }

        #endregion

        #region constructors

        public UserDbContext(): base()
        {
        }

        public UserDbContext(DbContextOptions<UserDbContext> options) : base(options)
        {
        }

        #endregion

        #region methods
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<WaterRate>(entity =>
            {
                entity.ToTable("water_rates");

                entity.HasKey(e => e.Id);

                entity.Property(e => e.Classification)
                    .HasConversion<int>();

                entity.Property(e => e.MeterSize)
                    .HasPrecision(4, 2);

                entity.Property(e => e.MinimumCharge)
                    .HasPrecision(12, 2);

                entity.Property(e => e.Rate11To20)
                    .HasPrecision(12, 2);

                entity.Property(e => e.Rate21To30)
                    .HasPrecision(12, 2);

                entity.Property(e => e.Rate31To40)
                    .HasPrecision(12, 2);

                entity.Property(e => e.Rate41Up)
                    .HasPrecision(12, 2);

                entity.Property(e => e.EffectiveDate)
                    .IsRequired();

                entity.Property(e => e.IsActive)
                    .IsRequired();

                entity.Property(e => e.DateCreated)
                    .IsRequired();

                entity.Property(e => e.DateUpdated)
                    .IsRequired();
            });
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));
            base.OnConfiguring(optionsBuilder);
        }

        #endregion
    }
}
