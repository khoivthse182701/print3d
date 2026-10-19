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
    public DbSet<PricingPackage> PricingPackages => Set<PricingPackage>();
    public DbSet<PartnerLab> PartnerLabs => Set<PartnerLab>();
    public DbSet<GenerationTask> GenerationTasks => Set<GenerationTask>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<Payment> Payments => Set<Payment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}
