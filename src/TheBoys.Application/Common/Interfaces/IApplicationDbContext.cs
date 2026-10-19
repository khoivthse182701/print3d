using Microsoft.EntityFrameworkCore;
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

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
