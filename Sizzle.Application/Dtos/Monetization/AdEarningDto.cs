namespace Sizzle.Application.Dtos.Monetization;

public sealed record AdEarningDto(
    Guid Id,
    int AdViewCount,
    decimal Amount,
    string Currency,
    string StripeTransferId,
    DateTime CreatedAt);
