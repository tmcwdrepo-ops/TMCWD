using Microsoft.EntityFrameworkCore;
using TMCWD.Data.Entities;

namespace TMCWD.Data.Context
{
    public class UserDbContext : DbContext
    {
        #region Fields
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

        public DbSet<PenaltyWaiverRequest> PenaltyWaiverRequests { get; set; }

        public DbSet<ChargeType> ChargeTypes { get; set; }

        public DbSet<OtherCharge> OtherCharges { get; set; }
        public DbSet<OtherChargeInstallment> OtherChargeInstallments { get; set; }

        public DbSet<BillingAdjustment> BillingAdjustments { get; set; }

        public DbSet<DebitCreditMemo> DebitCreditMemos { get; set; }

        public DbSet<PaymentCheck> PaymentChecks { get; set; }

        public DbSet<AdvancePayment> AdvancePayments { get; set; }

        public DbSet<WebHook> WebHooks { get; set; }

        public DbSet<Payment> Payments { get; set; }
        public DbSet<PaymentReceipt> PaymentReceipts { get; set; }

        public DbSet<CollectionRemittance> CollectionRemittances { get; set; }

        public DbSet<SeniorCitizenDiscount> SeniorCitizenDiscounts { get; set; }

        public DbSet<InvoiceCancellation> InvoiceCancellations { get; set; }

        public DbSet<EWalletTransaction> EWalletTransactions { get; set; }

        public DbSet<TMCWD.Data.Entities.Endpoint> Endpoints { get; set; }

        public DbSet<Reading> Readings { get; set; }

        public DbSet<Tariff> Tariffs { get; set; }

        public DbSet<WaterRate> WaterRates { get; set; }

        public DbSet<ZoneBook> ZoneBooks { get; set; }

        public DbSet<ReadingSheetTemplate> ReadingSheetTemplates { get; set; }

        public DbSet<BatchPaymentUpload> BatchPaymentUploads { get; set; }

        public DbSet<WaterConnectionApplication> WaterConnectionApplications { get; set; }

        public DbSet<DisconnectionOrder> DisconnectionOrders { get; set; }

        public DbSet<AccountTransferOrder> AccountTransferOrders { get; set; }

        // ── Phase 6D ──────────────────────────────────────────────────────────
        public DbSet<BirInvoiceConfig> BirInvoiceConfigs { get; set; }
        public DbSet<ServiceInvoiceSeq> ServiceInvoiceSeq { get; set; }

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
            modelBuilder.Entity<OtherChargeInstallment>(entity =>
            {
                entity.HasIndex(x => new { x.OtherChargeId, x.InstallmentNumber }).IsUnique();
                entity.HasIndex(x => new { x.PaymentStatus, x.DueBillingPeriod });
                entity.HasIndex(x => x.SettledPaymentId);
                entity.Property(x => x.ActivatedBillingReferenceId).HasMaxLength(50);
            });
            modelBuilder.Entity<PaymentReceipt>().HasIndex(x => x.PaymentReference).IsUnique().HasDatabaseName("UQ_payment_receipts_reference");

            modelBuilder.Entity<Request>().HasIndex(x => x.LegacyServiceRequestId).IsUnique();

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
            if (!optionsBuilder.IsConfigured)
            {
                throw new InvalidOperationException(
                    "UserDbContext must be created through dependency injection with a configured connection string.");
            }

            base.OnConfiguring(optionsBuilder);
        }

        #endregion
    }
}
