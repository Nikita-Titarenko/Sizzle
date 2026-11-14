using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace Sizzle.Infrastructure.Tests.Services;

internal sealed class TestEnvironment : IHostEnvironment
{
    public string EnvironmentName { get; set; } = Environments.Production;

    public string ApplicationName { get; set; } = "Sizzle.Infrastructure.Tests";

    public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}