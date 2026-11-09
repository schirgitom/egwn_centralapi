using EGWNCentral.Core.Domain.Entities;
using EGWNCentral.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EGWNCentral.Infrastructure.Messaging;

internal static class CustomerSnapshotBuilder
{
    public static IQueryable<EnergyCommunity> ReferencedCommunities(ApplicationDbContext context)
    {
        var ids = context.ChangeTracker.Entries<CommunityParticipation>()
            .Where(entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Select(entry => entry.Entity.EnergyCommunityId).Distinct().ToList();
        return context.EnergyCommunities.Where(community => ids.Contains(community.Id));
    }

    public static IQueryable<Customer> AffectedCustomers(ApplicationDbContext context)
    {
        context.ChangeTracker.DetectChanges();
        var changes = context.ChangeTracker.Entries()
            .Where(x => x.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .ToList();
        var customerIds = changes.Where(x => x.Entity is Customer).Select(x => ((Customer)x.Entity).Id).ToHashSet();
        var meterIds = changes.Where(x => x.Entity is MeteringPoint).Select(x => ((MeteringPoint)x.Entity).Id).ToHashSet();
        var communityIds = changes.Where(x => x.Entity is EnergyCommunity).Select(x => ((EnergyCommunity)x.Entity).Id).ToList();

        foreach (var entry in changes.Where(x => x.Entity is CommunityParticipation))
        {
            var meterId = entry.Property(nameof(CommunityParticipation.MeteringPointId));
            meterIds.Add((Guid)meterId.CurrentValue!);
            if (entry.State != EntityState.Added)
            {
                meterIds.Add((Guid)meterId.OriginalValue!);
            }
        }

        foreach (var entry in changes.Where(x => x.Metadata.Name == "CustomerMeteringPoints"))
        {
            customerIds.Add((Guid)entry.Property("CustomerId").CurrentValue!);
            if (entry.State != EntityState.Added)
            {
                customerIds.Add((Guid)entry.Property("CustomerId").OriginalValue!);
            }
        }

        var directIds = customerIds.ToList();
        var affectedMeterIds = meterIds.ToList();
        return context.Customers
            .AsSplitQuery()
            .Where(customer => directIds.Contains(customer.Id) ||
                customer.MeteringPoints.Any(meter => affectedMeterIds.Contains(meter.Id) ||
                    meter.CommunityParticipations.Any(participation => communityIds.Contains(participation.EnergyCommunityId))))
            .Include(customer => customer.MeteringPoints)
            .ThenInclude(meter => meter.CommunityParticipations)
            .ThenInclude(participation => participation.EnergyCommunity);
    }

    public static CustomerMasterData Build(ApplicationDbContext context, Customer customer)
    {
        var removedLinks = context.ChangeTracker.Entries()
            .Where(entry => entry.Metadata.Name == "CustomerMeteringPoints" && entry.State == EntityState.Deleted)
            .Select(entry => ((Guid)entry.Property("CustomerId").CurrentValue!,
                (Guid)entry.Property("MeteringPointId").CurrentValue!))
            .ToHashSet();
        var meters = customer.IsDeleted ? [] : customer.MeteringPoints
            .Where(meter => !meter.IsDeleted && !removedLinks.Contains((customer.Id, meter.Id)))
            .OrderBy(meter => meter.MeteringPointCode, StringComparer.Ordinal)
            .ThenBy(meter => meter.Id)
            .ToList();
        var communities = meters.SelectMany(meter => meter.CommunityParticipations)
            .Where(participation => !participation.IsDeleted && !participation.EnergyCommunity.IsDeleted)
            .GroupBy(participation => participation.EnergyCommunityId)
            .Select(group =>
            {
                var community = group.First().EnergyCommunity;
                return new CustomerEnergyCommunity(community.Id, community.Name, community.Source, community.SourceId,
                    group.OrderBy(participation => participation.MeteringPoint.MeteringPointCode, StringComparer.Ordinal)
                        .ThenBy(participation => participation.MeteringPointId)
                        .Select(participation => new CustomerCommunityMeteringPoint(
                            participation.MeteringPointId, participation.MeteringPoint.MeteringPointCode,
                            participation.MeteringPoint.EnergyDirection, participation.MeteringPoint.Description,
                            participation.ParticipationPercentage)).ToArray());
            })
            .OrderBy(community => community.Name, StringComparer.Ordinal)
            .ThenBy(community => community.Id)
            .ToArray();
        return new CustomerMasterData(customer.Id, customer.CustomerNumber, customer.RegistrationCode, customer.Name, customer.Email,
            customer.IsDeleted,
            meters.Select(meter => new CustomerMeteringPoint(meter.Id, meter.MeteringPointCode,
                meter.EnergyDirection, meter.Description)).ToArray(),
            communities);
    }
}
