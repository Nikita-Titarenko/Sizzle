using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Sizzle.Application.Repositories;

namespace Sizzle.Infrastructure.Repositories;

public class Repository<T>(ApplicationDbContext dbContext, ILogger<Repository<T>>? logger = null) : IRepository<T> where T : class
{
    private readonly ILogger<Repository<T>> logger = logger ?? NullLogger<Repository<T>>.Instance;

    protected ApplicationDbContext DbContext { get; } = dbContext;

    protected DbSet<T> DbSet { get; set; } = dbContext.Set<T>();

    public void Add(T entity)
    {
        this.logger.LogInformation("db.repository.add {Entity}", typeof(T).Name);
        _ = DbSet.Add(entity);
    }

    public Task<bool> Exists(params object[] keyValues) => Exists(CancellationToken.None, keyValues);

    public async Task<bool> Exists(CancellationToken cancellationToken, params object[] keyValues)
    {
        logger.LogInformation("db.repository.exists {Entity}", typeof(T).Name);
        var entity = await DbSet.FindAsync(keyValues, cancellationToken);
        return entity != null;
    }

    public void AddRange(IEnumerable<T> entities)
    {
        logger.LogInformation("db.repository.add_range {Entity}", typeof(T).Name);
        DbSet.AddRange(entities);
    }

    public void Remove(T entity)
    {
        logger.LogInformation("db.repository.remove {Entity}", typeof(T).Name);
        _ = DbSet.Remove(entity);
    }

    public Task<T?> GetAsync(params object[] keyValues) => GetAsync(CancellationToken.None, keyValues);

    public async Task<T?> GetAsync(CancellationToken cancellationToken, params object[] keyValues)
    {
        logger.LogInformation("db.repository.get_by_id {Entity}", typeof(T).Name);
        return await DbSet.FindAsync(keyValues, cancellationToken);
    }

    public async Task<IEnumerable<T>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("db.repository.get_all {Entity}", typeof(T).Name);
        return await DbSet.ToListAsync(cancellationToken);
    }
}
