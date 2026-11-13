using Google.Apis.Auth.OAuth2;
using Sizzle.Application.Dtos;
using Sizzle.Application.Services.Recipes;
using Sizzle.Infrastructure.Options;
using Sizzle.Infrastructure.Services;

namespace Sizzle.Infrastructure.Tests.Services;

public class GeminiServiceTests
{
    [Fact]
    public async Task GenerateImageAsync_RequiresProjectAndImageModel()
    {
        var service = new GeminiService(Microsoft.Extensions.Options.Options.Create(new GeminiOptions()),
            GoogleCredential.FromAccessToken("test-token"));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.GenerateImageAsync("test prompt"));

        Assert.Contains("Gemini:ImageModel", exception.Message);
    }

    [Fact]
    public async Task GenerateStreamAsync_RequiresProjectAndModel()
    {
        var service = new GeminiService(Microsoft.Extensions.Options.Options.Create(new GeminiOptions()),
            GoogleCredential.FromAccessToken("test-token"));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await foreach (var _ in service.GenerateStreamAsync("test prompt", [new AiMessageDto("user", [new AiPartDto(Text: "hello")])])) { }
        });

        Assert.Contains("Gemini:ProjectId", exception.Message);
    }
}