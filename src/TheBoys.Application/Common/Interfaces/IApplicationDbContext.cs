using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using TheBoys.Domain.Entities;

namespace TheBoys.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<User> Users { get; }
    DbSet<PricingPackage> PricingPackages { get; }
    DbSet<PartnerLab> PartnerLabs { get; }
    DbSet<GenerationTask> GenerationTasks { get; }
    DbSet<Order> Orders { get; }
    DbSet<Payment> Payments { get; }

    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
    Task<IDbContextTransaction> BeginTransactionAsync(IsolationLevel isolationLevel, CancellationToken cancellationToken = default);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
