namespace Sizzle.Application.Dtos.Subscriptions;

public sealed record StripePaymentDto(Guid OrderId, long AmountTotal, string Currency);