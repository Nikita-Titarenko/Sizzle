using Microsoft.EntityFrameworkCore;
using Sizzle.Application.Repositories;

namespace Sizzle.Infrastructure.Repositories;

public class Repository<T>(ApplicationDbContext dbContext) : IRepository<T> where T : class
{
    protected ApplicationDbContext DbContext { get; } = dbContext;

    protected DbSet<T> DbSet { get; set; } = dbContext.Set<T>();

    public void Add(T entity)
    {
        _ = DbSet.Add(entity);
    }

    public Task<bool> Exists(params object[] keyValues) => Exists(CancellationToken.None, keyValues);

    public async Task<bool> Exists(CancellationToken cancellationToken, params object[] keyValues)
    {
        var entity = await DbSet.FindAsync(keyValues, cancellationToken);
        return entity != null;
    }

    public void AddRange(IEnumerable<T> entities)
    {
        DbSet.AddRange(entities);
    }

    public void Remove(T entity)
    {
        _ = DbSet.Remove(entity);
    }

    public Task<T?> GetAsync(params object[] keyValues) => GetAsync(CancellationToken.None, keyValues);

    public async Task<T?> GetAsync(CancellationToken cancellationToken, params object[] keyValues)
    {
        return await DbSet.FindAsync(keyValues, cancellationToken);
    }

    public async Task<IEnumerable<T>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await DbSet.ToListAsync(cancellationToken);
    }
}
