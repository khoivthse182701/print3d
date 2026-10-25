using Microsoft.EntityFrameworkCore;
using TheBoys.Application.Common.Interfaces;
using TheBoys.Domain.Entities;

namespace TheBoys.Infrastructure.Persistence;

public sealed class ApplicationDbContext : DbContext, IApplicationDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<CreditAccount> CreditAccounts => Set<CreditAccount>();
    public DbSet<CreditLedgerEntry> CreditLedger => Set<CreditLedgerEntry>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<IdempotencyRequest> IdempotencyRequests => Set<IdempotencyRequest>();
    public DbSet<RefreshSession> RefreshSessions => Set<RefreshSession>();
    public DbSet<IdentityToken> IdentityTokens => Set<IdentityToken>();
    public DbSet<ConsentRecord> ConsentRecords => Set<ConsentRecord>();
    public DbSet<PricingPackage> PricingPackages => Set<PricingPackage>();
    public DbSet<PartnerLab> PartnerLabs => Set<PartnerLab>();
    public DbSet<GenerationTask> GenerationTasks => Set<GenerationTask>();
    public DbSet<ModelProject> ModelProjects => Set<ModelProject>();
    public DbSet<GenerationJob> GenerationJobs => Set<GenerationJob>();
    public DbSet<GenerationStage> GenerationStages => Set<GenerationStage>();
    public DbSet<ModelVersion> ModelVersions => Set<ModelVersion>();
    public DbSet<ModelAsset> ModelAssets => Set<ModelAsset>();
    public DbSet<PrintReview> PrintReviews => Set<PrintReview>();
    public DbSet<CustomerApproval> CustomerApprovals => Set<CustomerApproval>();
    public DbSet<Quote> Quotes => Set<Quote>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<OrderStatusHistory> OrderStatusHistories => Set<OrderStatusHistory>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<PaymentAttempt> PaymentAttempts => Set<PaymentAttempt>();
    public DbSet<PaymentEvent> PaymentEvents => Set<PaymentEvent>();
    public DbSet<Refund> Refunds => Set<Refund>();
    public DbSet<OrderCancellationRequest> OrderCancellationRequests => Set<OrderCancellationRequest>();
    public DbSet<PartnerAssignment> PartnerAssignments => Set<PartnerAssignment>();
    public DbSet<ProductionRun> ProductionRuns => Set<ProductionRun>();
    public DbSet<Shipment> Shipments => Set<Shipment>();
    public DbSet<ShipmentEvent> ShipmentEvents => Set<ShipmentEvent>();
    public DbSet<DefectReport> DefectReports => Set<DefectReport>();
    public DbSet<DefectEvidence> DefectEvidence => Set<DefectEvidence>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<PrivacyRequest> PrivacyRequests => Set<PrivacyRequest>();

    public Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
        => Database.BeginTransactionAsync(cancellationToken);

    public Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction> BeginTransactionAsync(System.Data.IsolationLevel isolationLevel, CancellationToken cancellationToken = default)
        => Database.BeginTransactionAsync(isolationLevel, cancellationToken);

    public async Task<bool> AcquireApplicationLockAsync(string resourceName, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        if (!Database.IsSqlServer())
        {
            return true;
        }

        var timeoutMs = (int)timeout.TotalMilliseconds;
        var resultParam = new Microsoft.Data.SqlClient.SqlParameter
        {
            ParameterName = "@result",
            SqlDbType = System.Data.SqlDbType.Int,
            Direction = System.Data.ParameterDirection.Output
        };
        var resourceParam = new Microsoft.Data.SqlClient.SqlParameter("@resource", resourceName);
        var timeoutParam = new Microsoft.Data.SqlClient.SqlParameter("@timeout", timeoutMs);

        await Database.ExecuteSqlRawAsync(
            "EXEC @result = sp_getapplock @Resource = @resource, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = @timeout",
            new object[] { resultParam, resourceParam, timeoutParam },
            cancellationToken);

        var code = (int)(resultParam.Value ?? -1);
        return code >= 0;
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);
        optionsBuilder.ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Enforce DATETIME2(3) precision matching SQL DDL
        configurationBuilder.Properties<DateTime>().HavePrecision(3);
        configurationBuilder.Properties<DateTime?>().HavePrecision(3);
    }
}
