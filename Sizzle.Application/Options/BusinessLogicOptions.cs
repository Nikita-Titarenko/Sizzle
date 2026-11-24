namespace Sizzle.Application.Options;

public class BusinessLogicOptions
{
    public int RecipeViewUpdateIntervalMinutes { get; set; }

    public int RequiredFollowersCount { get; set; } = 1000;

    public int RequiredViewsLast90Days { get; set; } = 100000;

    public int EligibilityWindowDays { get; set; } = 90;

    public int CurrencyMinorMultiplier { get; set; } = 100;

    public int AdViewUpdateIntervalMinutes { get; set; } = 10;

    public int AdPayoutProcessingIntervalMinutes { get; set; } = 15;

    public decimal AdPayoutPerView { get; set; }

    public string AdPayoutCurrency { get; set; } = "usd";

    public bool AIValidation { get; set; } = true;
}