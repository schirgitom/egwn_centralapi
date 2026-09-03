using EGWNCentral.Core.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EGWNCentral.Infrastructure.Persistence;

public static class SeedData
{
    public static void Seed(ApplicationDbContext context)
    {
        if (context.Customers.Any(x => x.CustomerNumber == "EGWN-01"))
        {
            return;
        }

        var customers = new[]
        {
            CustomerWithPoints("EGWN-01", "Friedrich Kristofferitsch", ("AT0082000000000000000000006179501", "consumption")),
            CustomerWithPoints("EGWN-02", "Christoph Pichler",
                ("AT0082000818400000000000001242601", "consumption"),
                ("AT0082000818400000000000001242702", "consumption"),
                ("AT0082000818400000000000001242701", "consumption")),
            CustomerWithPoints("EGWN-03", "Sonja Pöllabauer", ("AT0080000819000000000000000145187", "consumption")),
            CustomerWithPoints("EGWN-04", "Sarah Mayer", ("AT0082000818400000000000003436301", "consumption")),
            CustomerWithPoints("EGWN-05", "Vinzenz Scharler", ("AT0082000819200000000000000067001", "consumption")),
            CustomerWithPoints("EGWN-06", "Anna & Michael Tiefengraber", ("AT0082000819100000000000000729601", "consumption")),
            CustomerWithPoints("EGWN-07", "Werner Derler",
                ("AT0082000819100000000000000756301", "consumption"),
                ("AT0080000819100000202501230486936", "generation")),
            CustomerWithPoints("EGWN-08", "Peter Strassegger", ("AT0082000817200000000000002615001", "consumption")),
            CustomerWithPoints("EGWN-09", "Peter Klamminger", ("AT0080000819000000202411050471847", "generation")),
            CustomerWithPoints("EGWN-10", "Bernd Kerschbaumer",
                ("AT0080000819000000202012100079281", "generation"),
                ("AT0080000819100000000000000066587", "consumption")),
            CustomerWithPoints("EGWN-11", "Fritz jun. Kristofferitsch",
                ("AT0082000000000000000000006197701", "consumption"),
                ("AT0080000819000000202209200159119", "generation")),
            CustomerWithPoints("EGWN-12", "Harald Pfurtscheller e.U.", ("AT0082000819100000000000004160001", "consumption")),
            CustomerWithPoints("EGWN-13", "EDL Event GmbH",
                ("AT0080000819000000000000000130123", "consumption"),
                ("AT0080000819000000202209200159122", "generation"),
                ("AT0082000000000000000000006128401", "consumption"),
                ("AT0080000819000000202209200159120", "generation")),
            CustomerWithPoints("EGWN-14", "Johann Flicker",
                ("AT0082000819000000000000001480001", "consumption"),
                ("AT0080000819000000202412030483624", "generation")),
            CustomerWithPoints("EGWN-15", "Kleintierpraxis Anger", ("AT0082000818400000000000002728401", "consumption")),
            CustomerWithPoints("EGWN-16", "Fritz Kristofferitsch", ("AT0080000819000000000000000086514", "consumption")),
            CustomerWithPoints("EGWN-17", "Hubert Schweiger", ("AT0082000819100000000000000251101", "consumption")),
            CustomerWithPoints("EGWN-18", "Zäzilia Straußberger", ("AT0082000819000000000000001470901", "consumption")),
            CustomerWithPoints("EGWN-19", "Johann Höfler", ("AT0082000818400000000000003434801", "consumption"))
        };

        context.Customers.AddRange(customers);
        context.SaveChanges();
    }

    private static Customer CustomerWithPoints(string customerNumber, string name, params (string Code, string Direction)[] meterPoints)
    {
        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            CustomerNumber = customerNumber,
            Name = name,
            MeteringPoints = meterPoints.Select(point => new MeteringPoint
            {
                Id = Guid.NewGuid(),
                MeteringPointCode = point.Code,
                EnergyDirection = point.Direction
            }).ToList()
        };

        return customer;
    }
}
