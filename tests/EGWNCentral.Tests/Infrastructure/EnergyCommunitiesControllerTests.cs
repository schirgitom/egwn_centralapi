using EGWNCentral.Api.Contracts.EnergyCommunities;
using EGWNCentral.Api.Controllers;
using EGWNCentral.Infrastructure.Persistence;
using EGWNCentral.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

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
        var generationMeter = await new MeteringPointService(unitOfWork).CreateAsync([customer.Id], "AT-GENERATION", "generation", null);
        var community = await service.CreateAsync("Community", "EDA", "external-id");
        await service.CreateAsync("Other", "EEGFaktura");
        await service.SetParticipationAsync(community.Id, meter.Id, 50);
        await service.SetParticipationAsync(community.Id, generationMeter.Id, 75);
        context.ChangeTracker.Clear();
        var controller = new EnergyCommunitiesController(service);

        var result = await controller.GetEnergyCommunitiesPerSource("EDA", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.Single(Assert.IsType<EnergyCommunityResponse[]>(ok.Value));
        Assert.Equal(community.Id, response.Id);
        Assert.Equal("external-id", response.SourceId);
        var participation = Assert.Single(response.Participations, x => x.MeteringPointId == meter.Id);
        Assert.Equal(meter.Id, participation.MeteringPointId);
        Assert.Equal(customer.Id, Assert.Single(participation.Customers).Id);
        Assert.Equal("consumption", participation.EnergyDirection);
        Assert.Equal("generation", Assert.Single(response.Participations, x => x.MeteringPointId == generationMeter.Id).EnergyDirection);

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(response, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        var jsonParticipations = json.RootElement.GetProperty("participations").EnumerateArray().ToArray();
        Assert.Contains(jsonParticipations, x => x.GetProperty("energyDirection").GetString() == "consumption");
        Assert.Contains(jsonParticipations, x => x.GetProperty("energyDirection").GetString() == "generation");
    }

    [Fact]
    public async Task SetParticipation_ReturnsMeteringPointEnergyDirection()
    {
        await using var context = CreateDbContext();
        var unitOfWork = new UnitOfWork(context);
        var service = new EnergyCommunityService(unitOfWork);
        var customer = await new CustomerService(unitOfWork).CreateAsync("K-1", "Customer", null);
        var meter = await new MeteringPointService(unitOfWork).CreateAsync([customer.Id], "AT-GENERATION", "generation", null);
        var community = await service.CreateAsync("Community", "EDA");
        context.ChangeTracker.Clear();
        var controller = new EnergyCommunitiesController(service);

        var result = await controller.SetParticipation(
            community.Id, meter.Id, new SetCommunityParticipationRequest { ParticipationPercentage = 50 }, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal("generation", Assert.IsType<CommunityParticipationResponse>(ok.Value).EnergyDirection);
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
