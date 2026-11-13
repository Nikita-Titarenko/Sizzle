using System.Security.Cryptography;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Sizzle.Infrastructure.Options;

namespace Sizzle.Infrastructure.Tests.Services;

public class GoogleCloudServiceAccountTests
{
    [Fact]
    public void RootServiceAccountProvidesGoogleCredentials()
    {
        using var rsa = RSA.Create();
        var configuration = new ConfigurationManager();
        configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["GoogleCloudServiceAccount:type"] = "service_account",
            ["GoogleCloudServiceAccount:project_id"] = "test-project",
            ["GoogleCloudServiceAccount:client_email"] = "test@test-project.iam.gserviceaccount.com",
            ["GoogleCloudServiceAccount:private_key"] = rsa.ExportPkcs8PrivateKeyPem(),
            ["GoogleCloudServiceAccount:private_key_id"] = "test-key",
            ["GoogleCloudServiceAccount:token_uri"] = "https://oauth2.googleapis.com/token"
        });
        var services = new ServiceCollection();
        services.AddInfrastructureServices(configuration, new TestEnvironment());

        using var provider = services.BuildServiceProvider();
        var credential = provider.GetRequiredService<GoogleCredential>();

        Assert.IsType<ServiceAccountCredential>(credential.UnderlyingCredential);
    }

    [Fact]
    public void MissingRootServiceAccountFailsWithoutFileFallback()
    {
        var services = new ServiceCollection();
        services.AddInfrastructureServices(new ConfigurationManager(), new TestEnvironment());

        using var provider = services.BuildServiceProvider();

        var exception = Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<GoogleCredential>());
        Assert.Contains("GoogleCloudServiceAccount", exception.Message);
    }

    private sealed class TestEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;

        public string ApplicationName { get; set; } = "Sizzle.Infrastructure.Tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}