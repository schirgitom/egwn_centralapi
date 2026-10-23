using EGWNCentral.Core.Abstractions;
using EGWNCentral.Core.Abstractions.Persistence;
using EGWNCentral.Core.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EGWNCentral.Infrastructure.Services;

public sealed class EnergyCommunityService(IUnitOfWork unitOfWork) : IEnergyCommunityService
{
    public async Task<IReadOnlyCollection<EnergyCommunity>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await unitOfWork.EnergyCommunities.Query(asNoTracking: true)
            .Include(x => x.Participations)
            .ThenInclude(x => x.MeteringPoint)
            .ThenInclude(x => x.Customer)
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);
    }

    public Task<EnergyCommunity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return unitOfWork.EnergyCommunities.Query(asNoTracking: true)
            .Include(x => x.Participations)
            .ThenInclude(x => x.MeteringPoint)
            .ThenInclude(x => x.Customer)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<EnergyCommunity> CreateAsync(
        string name,
        string? source = null,
        CancellationToken cancellationToken = default)
    {
        var community = new EnergyCommunity
        {
            Name = Normalize(name),
            Source = NormalizeOptional(source)
        };
        await unitOfWork.EnergyCommunities.AddAsync(community, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return community;
    }

    public async Task<EnergyCommunity?> UpdateAsync(
        Guid id,
        string name,
        string? source,
        CancellationToken cancellationToken = default)
    {
        var community = await unitOfWork.EnergyCommunities.GetByIdAsync(id, cancellationToken);
        if (community is null)
        {
            return null;
        }

        community.Name = Normalize(name);
        community.Source = NormalizeOptional(source);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return community;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var community = await unitOfWork.EnergyCommunities.GetByIdAsync(id, cancellationToken);
        if (community is null)
        {
            return false;
        }

        unitOfWork.EnergyCommunities.Remove(community);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<CommunityParticipation> SetParticipationAsync(
        Guid communityId,
        Guid meteringPointId,
        decimal percentage,
        CancellationToken cancellationToken = default)
    {
        if (percentage is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(percentage), "Participation percentage must be between 0 and 100.");
        }

        var communityExists = await unitOfWork.EnergyCommunities.Query(asNoTracking: true)
            .AnyAsync(x => x.Id == communityId, cancellationToken);
        if (!communityExists)
        {
            throw new KeyNotFoundException($"Energy community '{communityId}' not found.");
        }

        var meteringPointExists = await unitOfWork.MeteringPoints.Query(asNoTracking: true)
            .AnyAsync(x => x.Id == meteringPointId, cancellationToken);
        if (!meteringPointExists)
        {
            throw new KeyNotFoundException($"Metering point '{meteringPointId}' not found.");
        }

        var participation = await unitOfWork.CommunityParticipations.Query()
            .FirstOrDefaultAsync(
                x => x.EnergyCommunityId == communityId && x.MeteringPointId == meteringPointId,
                cancellationToken);

        if (participation is null)
        {
            participation = new CommunityParticipation
            {
                EnergyCommunityId = communityId,
                MeteringPointId = meteringPointId,
                ParticipationPercentage = percentage
            };
            await unitOfWork.CommunityParticipations.AddAsync(participation, cancellationToken);
        }
        else
        {
            participation.ParticipationPercentage = percentage;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await unitOfWork.CommunityParticipations.Query(asNoTracking: true)
            .Include(x => x.MeteringPoint)
            .ThenInclude(x => x.Customer)
            .SingleAsync(x => x.Id == participation.Id, cancellationToken);
    }

    public async Task<bool> RemoveParticipationAsync(
        Guid communityId,
        Guid meteringPointId,
        CancellationToken cancellationToken = default)
    {
        var participation = await unitOfWork.CommunityParticipations.Query()
            .FirstOrDefaultAsync(
                x => x.EnergyCommunityId == communityId && x.MeteringPointId == meteringPointId,
                cancellationToken);

        if (participation is null)
        {
            return false;
        }

        unitOfWork.CommunityParticipations.Remove(participation);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static string Normalize(string value)
    {
        var normalized = value.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new ArgumentException("Value cannot be empty.", nameof(value));
        }

        return normalized;
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
