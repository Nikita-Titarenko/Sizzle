using Sizzle.Application.Dtos;
using Sizzle.Application.Dtos.Monetization;
using Sizzle.Domain.Common;

namespace Sizzle.Application.Services.Monetization;

public interface IMonetizationService
{
    Task<Result> ProcessWebhookAsync(string payload, string signature, CancellationToken cancellationToken = default);

    Task<Result<ConnectedAccountStatusDto>> GetConnectedAccountStatusAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<PageResponse<AdEarningDto>> GetEarningsAsync(Guid userId, int pageIndex, int pageSize, CancellationToken cancellationToken = default);

    Task ProcessAdEarningsAsync(CancellationToken cancellationToken = default);
}
