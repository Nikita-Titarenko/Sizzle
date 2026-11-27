using System.Threading;
using Microsoft.Extensions.Hosting;
using Sizzle.Application.Services.Recipes;

namespace Sizzle.Infrastructure.Services;

public sealed class AiValidationRuntimeToggle(IHostEnvironment hostEnvironment) : IAiValidationRuntimeToggle
{
    private const int NoOverride = -1;
    private const int Disabled = 0;
    private const int Enabled = 1;

    private int state = NoOverride;

    public bool IsDevelopmentEnvironment() => hostEnvironment.IsDevelopment();

    public bool IsEnabled(bool configuredValue)
    {
        var current = Volatile.Read(ref state);
        return current switch
        {
            Enabled => true,
            Disabled => false,
            _ => configuredValue
        };
    }

    public void Set(bool enabled)
    {
        Interlocked.Exchange(ref state, enabled ? Enabled : Disabled);
    }
}