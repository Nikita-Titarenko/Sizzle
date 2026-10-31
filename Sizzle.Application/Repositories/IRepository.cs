namespace Sizzle.Application.Repositories;

public interface IRepository<T> where T : class
{
    void Add(T entity);

    void AddRange(IEnumerable<T> entities);

    Task<bool> Exists(params object[] keyValues);
    Task<bool> Exists(CancellationToken cancellationToken, params object[] keyValues);

    Task<IEnumerable<T>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<T?> GetAsync(params object[] keyValues);
    Task<T?> GetAsync(CancellationToken cancellationToken, params object[] keyValues);

    void Remove(T entity);
}
