namespace Sizzle.Application.Dtos.Subscriptions;

public sealed record SubscriptionPlanDto(int Id, string Name, int DurationMonths, decimal Price, string Currency);