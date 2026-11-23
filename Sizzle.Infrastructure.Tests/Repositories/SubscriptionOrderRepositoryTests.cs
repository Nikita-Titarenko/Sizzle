using Sizzle.Domain.Entities;
using Sizzle.Infrastructure.Repositories;

namespace Sizzle.Infrastructure.Tests.Repositories;

public class SubscriptionOrderRepositoryTests : InMemoryRepositoryTest
{
    private static readonly DateTime Now = new(2026, 11, 22, 12, 0, 0, DateTimeKind.Utc);
    private readonly Guid userId = Guid.NewGuid();

    [Fact]
    public async Task ActivateAsync_WithoutActiveSubscription_StartsNow()
    {
        await using var db = CreateContext();
        var order = await AddOrderAsync(db, SubscriptionPlan.Monthly);

        var activated = await new SubscriptionOrderRepository(db).ActivateAsync(order.Id, Now);

        Assert.NotNull(activated);
        Assert.True(activated.IsPaid);
        Assert.Equal(Now, activated.StartsAt);
        Assert.Equal(Now.AddMonths(1), activated.ExpiresAt);
    }

    [Fact]
    public async Task ActivateAsync_WhileSubscribed_StartsWhenCurrentPeriodEnds()
    {
        await using var db = CreateContext();
        var currentEnd = Now.AddDays(20);
        await AddOrderAsync(db, SubscriptionPlan.Monthly, paidUntil: currentEnd);
        var order = await AddOrderAsync(db, SubscriptionPlan.Yearly);

        var activated = await new SubscriptionOrderRepository(db).ActivateAsync(order.Id, Now);

        Assert.NotNull(activated);
        Assert.Equal(currentEnd, activated.StartsAt);
        Assert.Equal(currentEnd.AddYears(1), activated.ExpiresAt);
    }

    [Fact]
    public async Task ActivateAsync_IgnoresExpiredAndOtherUsersOrders()
    {
        await using var db = CreateContext();
        await AddOrderAsync(db, SubscriptionPlan.Monthly, paidUntil: Now.AddDays(-3));
        await AddOrderAsync(db, SubscriptionPlan.Yearly, paidUntil: Now.AddMonths(5), user: Guid.NewGuid());
        var order = await AddOrderAsync(db, SubscriptionPlan.HalfYearly);

        var activated = await new SubscriptionOrderRepository(db).ActivateAsync(order.Id, Now);

        Assert.NotNull(activated);
        Assert.Equal(Now, activated.StartsAt);
        Assert.Equal(Now.AddMonths(6), activated.ExpiresAt);
    }

    [Fact]
    public async Task ActivateAsync_AlreadyPaidOrder_ReturnsNull()
    {
        await using var db = CreateContext();
        var order = await AddOrderAsync(db, SubscriptionPlan.Monthly, paidUntil: Now.AddDays(10));

        Assert.Null(await new SubscriptionOrderRepository(db).ActivateAsync(order.Id, Now));
    }

    private async Task<SubscriptionOrder> AddOrderAsync(
        ApplicationDbContext db, SubscriptionPlan plan, DateTime? paidUntil = null, Guid? user = null)
    {
        var order = new SubscriptionOrder
        {
            Id = Guid.NewGuid(),
            UserId = user ?? userId,
            Plan = plan,
            Amount = 99.99m,
            CreatedAt = Now.AddDays(-40),
            IsPaid = paidUntil != null,
            StartsAt = paidUntil?.AddMonths(-1),
            ExpiresAt = paidUntil
        };
        db.SubscriptionOrders.Add(order);
        await db.SaveChangesAsync();
        return order;
    }
}
