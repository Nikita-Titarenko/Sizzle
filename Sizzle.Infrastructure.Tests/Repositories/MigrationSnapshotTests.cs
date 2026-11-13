using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Sizzle.Infrastructure.Tests.Repositories;

public class MigrationSnapshotTests
{
    [Fact]
    public void SnapshotMatchesModel()
    {
        using var context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseMySQL("server=localhost;database=sizzle;user=test;password=test").Options);
        var snapshot = context.GetService<IMigrationsAssembly>().ModelSnapshot!;
        var differ = context.GetService<IMigrationsModelDiffer>();
        var source = context.GetService<IModelRuntimeInitializer>().Initialize(snapshot.Model, designTime: true).GetRelationalModel();
        var target = context.GetService<IDesignTimeModel>().Model.GetRelationalModel();
        var differences = differ.GetDifferences(source, target);
        Assert.True(differences.Count == 0, string.Join("; ", differences.Select(change =>
            $"{change.GetType().Name}: {change}")));
    }
}
