using EGWNCentral.Core.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EGWNCentral.Infrastructure.Persistence;

public static class SeedData
{
    public static void Seed(ApplicationDbContext context)
    {
        if (context.Customers.Any())
        {
            return;
        }

        var rows = ReadSeedRows();
        if (rows.Count == 0)
        {
            return;
        }

        var customersByKey = rows
            .GroupBy(x => x.ParticipantKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => CreateCustomer(group.Key, group.ToList()),
                StringComparer.OrdinalIgnoreCase);

        var energyCommunities = rows
            .Select(x => x.CommunityName)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(name => new EnergyCommunity
            {
                Id = Guid.NewGuid(),
                Name = name
            })
            .ToList();

        context.Customers.AddRange(customersByKey.Values);
        context.EnergyCommunities.AddRange(energyCommunities);
        context.SaveChanges();

        var communityByName = energyCommunities.ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);
        var meteringPointByCode = customersByKey.Values
            .SelectMany(x => x.MeteringPoints)
            .ToDictionary(x => x.MeteringPointCode, StringComparer.OrdinalIgnoreCase);

        var participations = rows
            .Where(x => !string.IsNullOrWhiteSpace(x.CommunityName))
            .Select(x => (x.CommunityName, x.MeteringPointCode))
            .Distinct()
            .Select(item => new CommunityParticipation
            {
                Id = Guid.NewGuid(),
                EnergyCommunityId = communityByName[item.CommunityName].Id,
                MeteringPointId = meteringPointByCode[item.MeteringPointCode].Id,
                ParticipationPercentage = 100m
            })
            .ToList();

        context.CommunityParticipations.AddRange(participations);
        context.SaveChanges();
    }

    private static Customer CreateCustomer(string participantKey, List<SeedRow> rows)
    {
        var first = rows[0];
        var customerNumber = $"EGWN-{participantKey}";
        var fullName = $"{first.FirstName} {first.LastName}".Trim();
        var name = string.IsNullOrWhiteSpace(fullName) ? $"Teilnehmer {participantKey}" : fullName;
        var email = rows.Select(x => x.Email).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));

        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            CustomerNumber = customerNumber,
            Name = name,
            Email = string.IsNullOrWhiteSpace(email) ? null : email,
            MeteringPoints = rows.Select(row => row.MeteringPointCode).Distinct(StringComparer.OrdinalIgnoreCase).Select(meterPointCode => new MeteringPoint
            {
                Id = Guid.NewGuid(),
                MeteringPointCode = meterPointCode,
                EnergyDirection = "consumption"
            }).ToList()
        };

        return customer;
    }

    private static List<SeedRow> ReadSeedRows()
    {
        var csvPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "data", "Seeding.csv");
        var fullPath = Path.GetFullPath(csvPath);
        if (!File.Exists(fullPath))
        {
            return [];
        }

        var lines = File.ReadAllLines(fullPath);
        if (lines.Length <= 1)
        {
            return [];
        }

        var rows = new List<SeedRow>();
        for (var i = 1; i < lines.Length; i++)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var columns = line.Split(';');
            if (columns.Length < 11)
            {
                continue;
            }

            var meteringPointCode = columns[10].Trim();
            if (!meteringPointCode.StartsWith("AT", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var exportId = columns[0].Trim();
            var participantId = columns[7].Trim();
            var participantKey = string.IsNullOrWhiteSpace(participantId) ? $"EXP-{exportId}" : participantId;

            rows.Add(new SeedRow(
                participantKey,
                columns[1].Trim(),
                columns[2].Trim(),
                columns[6].Trim(),
                columns[8].Trim(),
                meteringPointCode));
        }

        return rows;
    }

    private sealed record SeedRow(
        string ParticipantKey,
        string LastName,
        string FirstName,
        string Email,
        string CommunityName,
        string MeteringPointCode);
}
