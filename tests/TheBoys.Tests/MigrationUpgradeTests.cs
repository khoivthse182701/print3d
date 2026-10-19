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
            ?? "Server=localhost;Trusted_Connection=True;TrustServerCertificate=True") { InitialCatalog = dbName };
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

            var user = new User { Email = "migration@example.test", FullName = "Existing customer" };
            db.Users.Add(user);
            await db.SaveChangesAsync();
            await db.Database.MigrateAsync();
            db.ChangeTracker.Clear();

            Assert.Equal("Existing customer", (await db.Users.SingleAsync()).FullName);
            Assert.Empty(await db.Database.GetPendingMigrationsAsync());
            var applied = (await db.Database.GetAppliedMigrationsAsync()).ToArray();
            Assert.Contains("20261019080454_InitialCreate", applied);
            Assert.Contains("20261019080618_InitialSchema", applied);
            Assert.EndsWith("MergeMainSchemaAlignment", applied[^1]);
            // Also prove the final model's feature columns can be queried.
            Assert.Empty(await db.GenerationTasks.ToListAsync());
            Assert.Empty(await db.PricingPackages.ToListAsync());

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
