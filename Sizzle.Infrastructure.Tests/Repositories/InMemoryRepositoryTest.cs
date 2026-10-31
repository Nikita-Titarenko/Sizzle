using Microsoft.EntityFrameworkCore;
using Sizzle.Infrastructure;

namespace Sizzle.Infrastructure.Tests.Repositories;

public abstract class InMemoryRepositoryTest
{
    private readonly DbContextOptions<ApplicationDbContext> options = new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .Options;

    protected ApplicationDbContext CreateContext() => new(options);
}
