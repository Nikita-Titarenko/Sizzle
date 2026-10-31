using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Sizzle.Application.Dtos.Ingredients;
using Sizzle.Application.Dtos.Categories;
using Sizzle.Application.Repositories;
using Sizzle.Application.Services.Categories;
using Sizzle.Application.Services.Devices;
using Sizzle.Application.Services.Ingredients;
using Sizzle.Domain.Entities;

namespace Sizzle.Application.Tests.Services;

public class CancellationPropagationTests
{
    [Fact]
    public async Task CategoryService_ForwardsTokenToRepository()
    {
        using var source = new CancellationTokenSource();
        var repository = Substitute.For<ICategoryRepository>();
        var cache = Substitute.For<ICategoryReadCache>();
        cache.GetOrCreateAsync("en", Arg.Any<Func<CancellationToken, Task<IReadOnlyList<CategoryDto>>>>(), source.Token)
            .Returns(call => call.Arg<Func<CancellationToken, Task<IReadOnlyList<CategoryDto>>>>()(source.Token));
        repository.GetAllAsync(source.Token).Returns([]);

        await new CategoryService(repository, cache).GetAllAsync("en", source.Token);

        await cache.Received(1).GetOrCreateAsync("en", Arg.Any<Func<CancellationToken, Task<IReadOnlyList<CategoryDto>>>>(), source.Token);
        await repository.Received(1).GetAllAsync(source.Token);
    }

    [Fact]
    public async Task DeviceTokenService_ForwardsTokenToLookupAndCommit()
    {
        using var source = new CancellationTokenSource();
        var repository = Substitute.For<IDeviceTokenRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var service = new DeviceTokenService(repository, unitOfWork, NullLogger<DeviceTokenService>.Instance);

        await service.RegisterAsync(Guid.NewGuid(), "token", source.Token);

        await repository.Received(1).GetAsync("token", source.Token);
        repository.Received(1).Add(Arg.Any<DeviceToken>());
        await unitOfWork.Received(1).CommitAsync(source.Token);
    }

    [Fact]
    public async Task IngredientService_ForwardsTokenThroughCacheFactoryToRepository()
    {
        using var source = new CancellationTokenSource();
        var repository = Substitute.For<IIngredientRepository>();
        var cache = Substitute.For<IIngredientReadCache>();
        cache.GetOrCreateAsync(Arg.Any<GetAllIngredientsDto>(), Arg.Any<Func<Task<List<IngredientListItemDto>>>>())
            .Returns(call => call.Arg<Func<Task<List<IngredientListItemDto>>>>()());
        repository.GetAllOrderedAsync(Arg.Any<string?>(), Arg.Any<int?>(), Arg.Any<int?>(), "en", source.Token)
            .Returns([]);
        var service = new IngredientService(repository, Substitute.For<IIngredientUnitFormatter>(), cache);

        await service.GetAllAsync(new GetAllIngredientsDto { LanguageCode = "en" }, source.Token);

        await repository.Received(1).GetAllOrderedAsync(Arg.Any<string?>(), Arg.Any<int?>(), Arg.Any<int?>(), "en", source.Token);
    }
}
