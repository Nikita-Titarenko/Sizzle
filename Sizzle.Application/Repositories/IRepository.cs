namespace Sizzle.Application.Repositories;

public interface IRepository<T> where T : class
{
    void Add(T entity);

    void AddRange(IEnumerable<T> entities);

    Task<bool> Exists(params object[] keyValues);

    Task<IEnumerable<T>> GetAllAsync();

    Task<T?> GetAsync(params object[] keyValues);

    void Remove(T entity);
}
