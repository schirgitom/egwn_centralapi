using EGWNCentral.Core.Domain.Entities;

namespace EGWNCentral.Core.Abstractions;

public interface IMeteringPointService
{
    Task<IReadOnlyCollection<MeteringPoint>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<MeteringPoint?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<MeteringPoint> CreateAsync(IReadOnlyCollection<Guid> customerIds, string meteringPointCode, string energyDirection, string? description, CancellationToken cancellationToken = default);

    Task<MeteringPoint?> UpdateAsync(Guid id, string? description, string? energyDirection, IReadOnlyCollection<Guid>? customerIds = null, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
