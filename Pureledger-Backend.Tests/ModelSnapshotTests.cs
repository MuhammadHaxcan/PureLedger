using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using PureLedger.Data;
using Xunit;

namespace PureLedger.Backend.Tests;

public sealed class ModelSnapshotTests
{
    // Builds no connection: the check compares the code model with the last migration snapshot only.
    [Fact]
    public void CodeModel_MatchesTheLatestMigrationSnapshot()
    {
        using var context = new LedgerDbContext(new DbContextOptionsBuilder<LedgerDbContext>()
            .UseNpgsql("Host=localhost;Database=model-check;Username=x;Password=x").Options);
        var services = ((IInfrastructure<IServiceProvider>)context).Instance;
        var migrations = (IMigrationsAssembly)services.GetService(typeof(IMigrationsAssembly))!;
        var initializer = (IModelRuntimeInitializer)services.GetService(typeof(IModelRuntimeInitializer))!;
        var differ = (IMigrationsModelDiffer)services.GetService(typeof(IMigrationsModelDiffer))!;
        var designTime = (IDesignTimeModel)services.GetService(typeof(IDesignTimeModel))!;

        var snapshot = migrations.ModelSnapshot?.Model;
        Assert.NotNull(snapshot);
        if (snapshot is IMutableModel mutable) snapshot = mutable.FinalizeModel();
        snapshot = initializer.Initialize(snapshot);

        var differences = differ.GetDifferences(snapshot.GetRelationalModel(), designTime.Model.GetRelationalModel());

        Assert.True(differences.Count == 0,
            "Model changes without a migration: " + string.Join("; ", differences.Select(Describe)));
    }

    private static string Describe(MigrationOperation operation) => operation switch
    {
        AddColumnOperation x => $"add column {x.Table}.{x.Name}",
        DropColumnOperation x => $"drop column {x.Table}.{x.Name}",
        AlterColumnOperation x => $"alter column {x.Table}.{x.Name}",
        CreateTableOperation x => $"create table {x.Name}",
        DropTableOperation x => $"drop table {x.Name}",
        CreateIndexOperation x => $"create index {x.Name}",
        DropIndexOperation x => $"drop index {x.Name}",
        AddForeignKeyOperation x => $"add FK {x.Name}",
        DropForeignKeyOperation x => $"drop FK {x.Name}",
        AddCheckConstraintOperation x => $"add check {x.Name}",
        DropCheckConstraintOperation x => $"drop check {x.Name}",
        _ => operation.GetType().Name
    };
}
