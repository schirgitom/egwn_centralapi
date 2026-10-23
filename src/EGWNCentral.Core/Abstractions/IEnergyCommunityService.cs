using EGWNCentral.Core.Domain.Entities;

namespace EGWNCentral.Core.Abstractions;

public interface IEnergyCommunityService
{
    Task<IReadOnlyCollection<EnergyCommunity>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<EnergyCommunity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<EnergyCommunity> CreateAsync(string name, string? source = null, CancellationToken cancellationToken = default);

    Task<EnergyCommunity?> UpdateAsync(Guid id, string name, string? source, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task<CommunityParticipation> SetParticipationAsync(Guid communityId, Guid meteringPointId, decimal percentage, CancellationToken cancellationToken = default);

    Task<bool> RemoveParticipationAsync(Guid communityId, Guid meteringPointId, CancellationToken cancellationToken = default);
}
