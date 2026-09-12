using Sizzle.Application.Repositories;

namespace Sizzle.Infrastructure.Repositories;

public sealed class UnitOfWork(ApplicationDbContext dbContext) : IUnitOfWork, IDisposable
{
    private readonly ApplicationDbContext _dbContext = dbContext;

    public async Task CommitAsync()
    {
        _ = await _dbContext.SaveChangesAsync();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
