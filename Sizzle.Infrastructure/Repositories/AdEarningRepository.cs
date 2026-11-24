using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Sizzle.Application.Dtos;
using Sizzle.Application.Repositories;
using Sizzle.Domain.Entities;

namespace Sizzle.Infrastructure.Repositories;

public sealed class AdEarningRepository(ApplicationDbContext dbContext, ILogger<AdEarningRepository>? logger = null) : IAdEarningRepository
{
    private readonly ILogger<AdEarningRepository> logger = logger ?? NullLogger<AdEarningRepository>.Instance;

    public async Task<PageResponse<AdEarning>> GetByUserAsync(Guid userId, int pageIndex, int pageSize, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("db.ad_earnings.get_by_user");
        var query = dbContext.AdEarnings.AsNoTracking().Where(earning => earning.UserId == userId);
        var totalSize = await query.CountAsync(cancellationToken);
        var offset = (long)pageIndex * pageSize;
        if (offset > int.MaxValue)
        {
            return new PageResponse<AdEarning>
            {
                Data = [],
                TotalSize = totalSize,
                PageIndex = pageIndex,
                PageSize = pageSize
            };
        }

        var data = await query
            .OrderByDescending(earning => earning.CreatedAt)
            .ThenBy(earning => earning.Id)
            .Skip((int)offset)
            .Take(pageSize)
            .ToArrayAsync(cancellationToken);

        return new PageResponse<AdEarning>
        {
            Data = data,
            TotalSize = totalSize,
            PageIndex = pageIndex,
            PageSize = pageSize
        };
    }

    public void Add(AdEarning earning)
    {
        logger.LogInformation("db.ad_earnings.add");
        dbContext.AdEarnings.Add(earning);
    }
}
