namespace Sizzle.Application.Services.Recipes;

public interface IAiValidationRuntimeToggle
{
    bool IsDevelopmentEnvironment();

    bool IsEnabled(bool configuredValue);

    void Set(bool enabled);
}