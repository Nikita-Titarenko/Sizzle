using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sizzle.Application.Services.Notifications;
using Sizzle.Infrastructure.Options;

namespace Sizzle.Infrastructure.Services;

/// <summary>Sends push notifications through the Firebase Cloud Messaging HTTP v1 API.
/// Does nothing (and logs once) when Firebase is not configured, so the API runs without it.</summary>
public sealed class FcmPushNotificationSender(
    IHttpClientFactory httpClientFactory,
    IOptions<FirebaseOptions> options,
    ILogger<FcmPushNotificationSender> logger) : IPushNotificationSender
{
    private const string MessagingScope = "https://www.googleapis.com/auth/firebase.messaging";
    private const int MaxParallelRequests = 8;

    private readonly FirebaseOptions _options = options.Value;
    private readonly Lazy<ICredential?> _credential = new(() => LoadCredential(options.Value, logger));

    public async Task<IReadOnlyCollection<string>> SendAsync(IReadOnlyCollection<string> tokens,
        IReadOnlyDictionary<string, string> data, CancellationToken cancellationToken = default)
    {
        if (tokens.Count == 0 || _credential.Value is not { } credential)
        {
            return [];
        }

        var accessToken = await credential.GetAccessTokenForRequestAsync(cancellationToken: cancellationToken);
        var url = $"https://fcm.googleapis.com/v1/projects/{_options.ProjectId}/messages:send";
        var client = httpClientFactory.CreateClient(nameof(FcmPushNotificationSender));
        var invalidTokens = new ConcurrentBag<string>();

        await Parallel.ForEachAsync(tokens,
            new ParallelOptions { MaxDegreeOfParallelism = MaxParallelRequests, CancellationToken = cancellationToken },
            async (token, ct) =>
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = JsonContent.Create(new
                    {
                        message = new
                        {
                            token,
                            data,
                            // Data-only messages need high priority to wake the app and show a notification promptly.
                            android = new { priority = "HIGH" }
                        }
                    })
                };
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                try
                {
                    using var response = await client.SendAsync(request, ct);
                    if (response.IsSuccessStatusCode)
                    {
                        return;
                    }

                    var body = await response.Content.ReadAsStringAsync(ct);
                    if (response.StatusCode == HttpStatusCode.NotFound || body.Contains("UNREGISTERED", StringComparison.Ordinal))
                    {
                        invalidTokens.Add(token);
                    }
                    else
                    {
                        logger.LogWarning("FCM rejected a message ({Status}): {Body}", (int)response.StatusCode, body);
                    }
                }
                catch (HttpRequestException exception)
                {
                    logger.LogWarning(exception, "Failed to reach FCM");
                }
            });

        return invalidTokens;
    }

    private static ICredential? LoadCredential(FirebaseOptions options, ILogger logger)
    {
        if (string.IsNullOrWhiteSpace(options.ProjectId) || string.IsNullOrWhiteSpace(options.ServiceAccountFile))
        {
            logger.LogInformation("Firebase is not configured (Firebase:ProjectId, Firebase:ServiceAccountFile); push notifications are off");
            return null;
        }

        if (!File.Exists(options.ServiceAccountFile))
        {
            logger.LogError("Firebase service account file {Path} was not found; push notifications are off", options.ServiceAccountFile);
            return null;
        }

        using var stream = File.OpenRead(options.ServiceAccountFile);
        return GoogleCredential.FromServiceAccountCredential(ServiceAccountCredential.FromServiceAccountData(stream))
            .CreateScoped(MessagingScope);
    }
}
