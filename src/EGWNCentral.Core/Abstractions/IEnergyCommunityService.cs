using EGWNCentral.Core.Domain.Entities;

namespace EGWNCentral.Core.Abstractions;

public interface IEnergyCommunityService
{
    Task<IReadOnlyCollection<EnergyCommunity>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<EnergyCommunity>> GetBySourceAsync(string source, CancellationToken cancellationToken = default);

    Task<EnergyCommunity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<EnergyCommunity> CreateAsync(string name, string? source = null, string? sourceId = null, CancellationToken cancellationToken = default);

    Task<EnergyCommunity?> UpdateAsync(Guid id, string name, string? source, string? sourceId, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task<CommunityParticipation> SetParticipationAsync(Guid communityId, Guid meteringPointId, decimal percentage, CancellationToken cancellationToken = default);

    Task<bool> RemoveParticipationAsync(Guid communityId, Guid meteringPointId, CancellationToken cancellationToken = default);
}
