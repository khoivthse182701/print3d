using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;
using TheBoys.Domain.Entities;
using TheBoys.Infrastructure.Persistence;

namespace TheBoys.Tests;

public class MigrationUpgradeTests
{
    [Theory]
    [InlineData("20261019080454_InitialCreate")]
    [InlineData("20261019080618_InitialSchema")]
    [InlineData("20261019081829_MeshyPrintingDemo")]
    public async Task PublishedBranchDatabasesUpgradeWithoutRecreatingTables(string previousMigration)
    {
        var dbName = "TheBoys_Test_" + Guid.NewGuid().ToString("N");
        var connection = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("THEBOYS_TEST_SQL")
            ?? "Server=(localdb)\\MSSQLLocalDB;Trusted_Connection=True;TrustServerCertificate=True") { InitialCatalog = dbName };
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(connection.ConnectionString).Options);
        try
        {
            var migrator = db.GetService<IMigrator>();
            if (previousMigration.EndsWith("InitialCreate"))
                await migrator.MigrateAsync(previousMigration);
            else
            {
                // Reproduce the published feature branch: its history has no InitialCreate.
                await db.GetService<IRelationalDatabaseCreator>().CreateAsync();
                await db.Database.ExecuteSqlRawAsync(db.GetService<IHistoryRepository>().GetCreateScript());
                var assembly = db.GetService<IMigrationsAssembly>();
                foreach (var entry in assembly.Migrations.Where(x =>
                             string.CompareOrdinal(x.Key, "20261019080454_InitialCreate") > 0 &&
                             string.CompareOrdinal(x.Key, previousMigration) <= 0))
                {
                    var migration = assembly.CreateMigration(entry.Value, db.Database.ProviderName!);
                    var model = db.GetService<IModelRuntimeInitializer>().Initialize(migration.TargetModel);
                    var commands = db.GetService<IMigrationsSqlGenerator>()
                        .Generate(migration.UpOperations, model);
                    foreach (var command in commands)
                        await command.ExecuteNonQueryAsync(db.GetService<IRelationalConnection>());
                    await db.Database.ExecuteSqlRawAsync(db.GetService<IHistoryRepository>()
                        .GetInsertScript(new HistoryRow(entry.Key, "10.0.13")));
                }
            }

            var userId = Guid.NewGuid();
            await db.Database.ExecuteSqlRawAsync(@"
                INSERT INTO Users (Id, Email, FullName, Role, FreeTrialRemaining, PurchasedRemaining, TotalUsed, CreatedAtUtc, IsDeleted, IsPhoneVerified)
                VALUES ({0}, 'migration@example.test', 'Existing customer', 0, 2, 0, 0, SYSUTCDATETIME(), 0, 0)", userId);

            await db.Database.MigrateAsync();
            db.ChangeTracker.Clear();

            var upgradedUser = await db.Users.Include(u => u.CreditAccount).SingleAsync();
            Assert.Equal("Existing customer", upgradedUser.FullName);
            Assert.Equal("MIGRATION@EXAMPLE.TEST", upgradedUser.NormalizedEmail);
            Assert.Equal(Domain.Enums.AccountStatus.Active, upgradedUser.AccountStatus);
            Assert.NotNull(upgradedUser.CreditAccount);
            Assert.Equal(2, upgradedUser.CreditAccount.TrialBalance);
            Assert.Equal(0, upgradedUser.CreditAccount.PurchasedBalance);

            var ledgerEntries = await db.CreditLedger.Where(cl => cl.UserId == upgradedUser.Id).ToListAsync();
            Assert.Single(ledgerEntries);
            Assert.Equal(2, ledgerEntries[0].Delta);
            Assert.Equal(Domain.Enums.CreditBucket.Trial, ledgerEntries[0].Bucket);
            Assert.Equal(Domain.Enums.CreditLedgerKind.OpeningBalance, ledgerEntries[0].Kind);

            // Also prove the final model's feature columns can be queried.
            Assert.Empty(await db.GenerationTasks.ToListAsync());
            Assert.Empty(await db.PricingPackages.ToListAsync());
            Assert.Empty(await db.AuditLogs.ToListAsync());
            Assert.Empty(await db.OutboxMessages.ToListAsync());
            Assert.Empty(await db.IdempotencyRequests.ToListAsync());
            Assert.Empty(await db.RefreshSessions.ToListAsync());
            Assert.Empty(await db.IdentityTokens.ToListAsync());
            Assert.Empty(await db.ConsentRecords.ToListAsync());
            Assert.Empty(await db.ModelProjects.ToListAsync());
            Assert.Empty(await db.GenerationJobs.ToListAsync());
            Assert.Empty(await db.GenerationStages.ToListAsync());
            Assert.Empty(await db.ModelVersions.ToListAsync());
            Assert.Empty(await db.ModelAssets.ToListAsync());
            Assert.Empty(await db.PrintReviews.ToListAsync());
            Assert.Empty(await db.CustomerApprovals.ToListAsync());
            Assert.Empty(await db.Quotes.ToListAsync());
            Assert.Empty(await db.OrderItems.ToListAsync());
            Assert.Empty(await db.OrderStatusHistories.ToListAsync());
            Assert.Empty(await db.PaymentAttempts.ToListAsync());
            Assert.Empty(await db.PaymentEvents.ToListAsync());

            await migrator.MigrateAsync("0");
            Assert.Empty(await db.Database.GetAppliedMigrationsAsync());
        }
        finally
        {
            if (db.Database.GetDbConnection().Database != dbName || !dbName.StartsWith("TheBoys_Test_"))
                throw new InvalidOperationException("Unsafe test database cleanup");
            await db.Database.EnsureDeletedAsync();
        }
    }
}
