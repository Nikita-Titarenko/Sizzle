namespace Sizzle.Application.Repositories;

public interface IUnitOfWork
{
    Task CommitAsync();
}
