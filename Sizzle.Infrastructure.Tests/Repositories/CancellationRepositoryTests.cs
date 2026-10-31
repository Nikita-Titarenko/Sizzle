using Sizzle.Domain.Entities;
using Sizzle.Infrastructure.Repositories;

namespace Sizzle.Infrastructure.Tests.Repositories;

public class CancellationRepositoryTests : InMemoryRepositoryTest
{
    [Fact]
    public async Task CategoryRepository_PropagatesCanceledTokenToEfCore()
    {
        await using var db = CreateContext();
        using var source = new CancellationTokenSource();
        source.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new CategoryRepository(db).GetAllAsync(source.Token));
    }

    [Fact]
    public async Task UnitOfWork_PropagatesCanceledTokenToSaveChanges()
    {
        await using var db = CreateContext();
        db.Categories.Add(new Category { Id = Guid.NewGuid() });
        using var source = new CancellationTokenSource();
        source.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new UnitOfWork(db).CommitAsync(source.Token));
    }
}
