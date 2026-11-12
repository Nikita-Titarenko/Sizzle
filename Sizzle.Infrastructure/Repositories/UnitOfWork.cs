using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Sizzle.Application.Repositories;

namespace Sizzle.Infrastructure.Repositories;

public sealed class UnitOfWork(ApplicationDbContext dbContext, ILogger<UnitOfWork>? logger = null) : IUnitOfWork, IDisposable
{
    private readonly ApplicationDbContext _dbContext = dbContext;
    private readonly ILogger<UnitOfWork> logger = logger ?? NullLogger<UnitOfWork>.Instance;

    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("db.unit_of_work.commit");
        _ = await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
