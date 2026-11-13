using Sizzle.Domain.Entities;

namespace Sizzle.Application.Dtos.Subscriptions;

public sealed record SubscriptionHistoryDto(
	Guid OrderId,
	SubscriptionPlan Plan,
	decimal Amount,
	string Currency,
	DateTime StartsAt,
	DateTime ExpiresAt);