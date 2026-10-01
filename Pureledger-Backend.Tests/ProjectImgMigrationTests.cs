using Microsoft.EntityFrameworkCore;
using Npgsql;
using PureLedger.Data;
using PureLedger.Models;
using Xunit;

namespace PureLedger.Backend.Tests;

public sealed class ProjectImgMigrationTests
{
    [PostgresFact]
    public async Task Migration_CreatesOneLogoPerProjectAndRemovesOldColumn()
    {
        var connection = Environment.GetEnvironmentVariable("PURELEDGER_TEST_POSTGRES")!;
        var options = new DbContextOptionsBuilder<LedgerDbContext>().UseNpgsql(connection).Options;
        await using var context = new LedgerDbContext(options);

        Assert.Contains("20260928215754_AddProjectImg", await context.Database.GetAppliedMigrationsAsync());
        await using (var command = context.Database.GetDbConnection().CreateCommand())
        {
            await context.Database.OpenConnectionAsync();
            command.CommandText = "SELECT count(*) FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'ProjectCartageSettings' AND column_name = 'ProjectImage'";
            Assert.Equal(0L, (long)(await command.ExecuteScalarAsync())!);
        }

        await using var transaction = await context.Database.BeginTransactionAsync();
        var project = new Project { ProjectName = $"Logo migration test {Guid.NewGuid():N}" };
        context.Projects.Add(project);
        await context.SaveChangesAsync();
        context.ProjectImgs.Add(new ProjectImg { ProjectId = project.ProjectId, ImageDataUrl = "data:image/png;base64,test" });
        await context.SaveChangesAsync();

        var duplicate = await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlRawAsync(
            "INSERT INTO \"ProjectImg\" (\"ProjectId\", \"ImageDataUrl\") VALUES ({0}, {1})",
            project.ProjectId, "data:image/png;base64,duplicate"));
        Assert.Equal(PostgresErrorCodes.UniqueViolation, duplicate.SqlState);
    }
}
