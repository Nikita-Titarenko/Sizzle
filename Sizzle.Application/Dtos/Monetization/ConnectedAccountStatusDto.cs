namespace Sizzle.Application.Dtos.Monetization;

public sealed record ConnectedAccountStatusDto(
    bool IsEligible,
    int FollowersCount,
    int RecipeViewsLast90Days,
    int RequiredFollowersCount,
    int RequiredViewsLast90Days,
    bool IsStripeProfileConfigured,
    bool PayoutsEnabled,
    string? ConnectedAccountId,
    string? OnboardingLink,
    string? ExpressDashboardLoginLink);
