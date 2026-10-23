using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using TheBoys.Domain.Entities;

namespace TheBoys.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<User> Users { get; }
    DbSet<CreditAccount> CreditAccounts { get; }
    DbSet<CreditLedgerEntry> CreditLedger { get; }
    DbSet<AuditLog> AuditLogs { get; }
    DbSet<OutboxMessage> OutboxMessages { get; }
    DbSet<IdempotencyRequest> IdempotencyRequests { get; }
    DbSet<RefreshSession> RefreshSessions { get; }
    DbSet<IdentityToken> IdentityTokens { get; }
    DbSet<ConsentRecord> ConsentRecords { get; }
    DbSet<PricingPackage> PricingPackages { get; }
    DbSet<PartnerLab> PartnerLabs { get; }
    DbSet<GenerationTask> GenerationTasks { get; }
    DbSet<ModelProject> ModelProjects { get; }
    DbSet<GenerationJob> GenerationJobs { get; }
    DbSet<GenerationStage> GenerationStages { get; }
    DbSet<ModelVersion> ModelVersions { get; }
    DbSet<ModelAsset> ModelAssets { get; }
    DbSet<PrintReview> PrintReviews { get; }
    DbSet<CustomerApproval> CustomerApprovals { get; }
    DbSet<Quote> Quotes { get; }
    DbSet<Order> Orders { get; }
    DbSet<OrderItem> OrderItems { get; }
    DbSet<OrderStatusHistory> OrderStatusHistories { get; }
    DbSet<Payment> Payments { get; }
    DbSet<PaymentAttempt> PaymentAttempts { get; }
    DbSet<PaymentEvent> PaymentEvents { get; }

    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
    Task<IDbContextTransaction> BeginTransactionAsync(IsolationLevel isolationLevel, CancellationToken cancellationToken = default);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
