namespace Sizzle.Application.Dtos.Subscriptions;

public sealed record SubscriptionStatusDto(long RemainingSeconds, IReadOnlyList<SubscriptionHistoryDto> History);