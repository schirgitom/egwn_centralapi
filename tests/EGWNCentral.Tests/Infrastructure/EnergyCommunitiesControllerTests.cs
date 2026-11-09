using EGWNCentral.Api.Contracts.EnergyCommunities;
using EGWNCentral.Api.Controllers;
using EGWNCentral.Infrastructure.Persistence;
using EGWNCentral.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EGWNCentral.Tests.Infrastructure;

public sealed class EnergyCommunitiesControllerTests
{
    [Fact]
    public async Task GetEnergyCommunitiesPerSource_ReturnsMatchingResponseWithParticipations()
    {
        await using var context = CreateDbContext();
        var unitOfWork = new UnitOfWork(context);
        var service = new EnergyCommunityService(unitOfWork);
        var customer = await new CustomerService(unitOfWork).CreateAsync("K-1", "Customer", null);
        var meter = await new MeteringPointService(unitOfWork).CreateAsync([customer.Id], "AT-SHARED", "consumption", null);
        var community = await service.CreateAsync("Community", "EDA", "external-id");
        await service.CreateAsync("Other", "EEGFaktura");
        await service.SetParticipationAsync(community.Id, meter.Id, 50);
        context.ChangeTracker.Clear();
        var controller = new EnergyCommunitiesController(service);

        var result = await controller.GetEnergyCommunitiesPerSource("EDA", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.Single(Assert.IsType<EnergyCommunityResponse[]>(ok.Value));
        Assert.Equal(community.Id, response.Id);
        Assert.Equal("external-id", response.SourceId);
        var participation = Assert.Single(response.Participations);
        Assert.Equal(meter.Id, participation.MeteringPointId);
        Assert.Equal(customer.Id, Assert.Single(participation.Customers).Id);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task GetEnergyCommunitiesPerSource_RejectsMissingSource(string? source)
    {
        await using var context = CreateDbContext();
        var controller = new EnergyCommunitiesController(new EnergyCommunityService(new UnitOfWork(context)));
        var result = await controller.GetEnergyCommunitiesPerSource(source, CancellationToken.None);
        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"egwn-controller-tests-{Guid.NewGuid()}")
            .Options;
        return new ApplicationDbContext(options);
    }
}
