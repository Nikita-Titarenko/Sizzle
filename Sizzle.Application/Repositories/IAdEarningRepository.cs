using Sizzle.Application.Dtos;
using Sizzle.Domain.Entities;

namespace Sizzle.Application.Repositories;

public interface IAdEarningRepository
{
    Task<PageResponse<AdEarning>> GetByUserAsync(Guid userId, int pageIndex, int pageSize, CancellationToken cancellationToken = default);

    void Add(AdEarning earning);
}
