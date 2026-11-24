using EGWNCentral.Api.Controllers;
using EGWNCentral.Api.Services;
using EGWNCentral.Infrastructure.Persistence;
using EGWNCentral.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UglyToad.PdfPig;

namespace EGWNCentral.Tests.Infrastructure;

public sealed class RegistrationDocumentControllerTests
{
    [Fact]
    public async Task Get_ReturnsCurrentCustomerCodeAndLogoWithoutChangingIt()
    {
        await using var context = CreateDbContext();
        var service = new CustomerService(new UnitOfWork(context));
        var customer = await service.CreateAsync("K-123", "Müller Energie", "kunde@example.com");
        var controller = CreateController(service);

        var result = Assert.IsType<FileContentResult>(await controller.Get(" K-123 ", CancellationToken.None));

        Assert.Equal("application/pdf", result.ContentType);
        Assert.Equal("registrierung.pdf", result.FileDownloadName);
        Assert.Equal("private, no-store", controller.Response.Headers.CacheControl.ToString());
        Assert.NotEmpty(typeof(RegistrationDocumentController).GetCustomAttributes(typeof(AuthorizeAttribute), true));
        using var pdf = PdfDocument.Open(result.FileContents);
        var page = Assert.Single(pdf.GetPages());
        Assert.Contains("Müller Energie", page.Text);
        Assert.Contains("K-123", page.Text);
        Assert.Contains(customer.RegistrationCode, page.Text);
        var image = Assert.Single(page.GetImages());
        Assert.InRange(Math.Abs((image.Bounds.Left + image.Bounds.Right) / 2 - page.Width / 2), 0, 0.1);
        Assert.InRange(image.Bounds.Width, 149, 151);
        Assert.Contains("Geben Sie diesen Code bei der Registrierung im Feld „Registrierungscode“ ein.", page.Text);
        Assert.Equal(customer.RegistrationCode, (await service.GetByNumberAsync("K-123"))!.RegistrationCode);

        var newCode = await service.RotateRegistrationCodeAsync(customer.Id);
        var updatedFile = Assert.IsType<FileContentResult>(await controller.Get("K-123", CancellationToken.None));
        using var updatedPdf = PdfDocument.Open(updatedFile.FileContents);
        Assert.Contains(newCode!, updatedPdf.GetPage(1).Text);
    }

    [Fact]
    public async Task Get_RejectsMissingAndDeletedCustomers()
    {
        await using var context = CreateDbContext();
        var service = new CustomerService(new UnitOfWork(context));
        var customer = await service.CreateAsync("K-1", "Customer", null);
        var controller = CreateController(service);

        Assert.IsType<BadRequestObjectResult>(await controller.Get(" ", CancellationToken.None));
        Assert.IsType<NotFoundResult>(await controller.Get("unknown", CancellationToken.None));
        await service.DeleteAsync(customer.Id);
        Assert.IsType<NotFoundResult>(await controller.Get("K-1", CancellationToken.None));
    }

    [Fact]
    public async Task Get_ContainsLongCustomerDataWithinThePage()
    {
        await using var context = CreateDbContext();
        var service = new CustomerService(new UnitOfWork(context));
        var number = new string('W', 64);
        var name = new string('W', 200);
        await service.CreateAsync(number, name, null);

        var result = Assert.IsType<FileContentResult>(await CreateController(service).Get(number, CancellationToken.None));
        using var pdf = PdfDocument.Open(result.FileContents);
        var page = Assert.Single(pdf.GetPages());
        Assert.Contains(number, page.Text);
        Assert.Contains(name, page.Text);
        Assert.All(page.Letters, letter =>
        {
            Assert.InRange(letter.GlyphRectangle.Left, 0, page.Width);
            Assert.InRange(letter.GlyphRectangle.Right, 0, page.Width);
            Assert.InRange(letter.GlyphRectangle.Bottom, 0, page.Height);
            Assert.InRange(letter.GlyphRectangle.Top, 0, page.Height);
        });
    }

    [Fact]
    public async Task Get_ShowsOnlyActiveCustomerMetersAndTheirActiveCommunities()
    {
        await using var context = CreateDbContext();
        var unit = new UnitOfWork(context);
        var customers = new CustomerService(unit);
        var meters = new MeteringPointService(unit);
        var communities = new EnergyCommunityService(unit);
        var customer = await customers.CreateAsync("K-1", "Kunde", null);
        var other = await customers.CreateAsync("K-2", "Andere", null);
        var shared = await meters.CreateAsync([customer.Id, other.Id], "AT-SHARED", "consumption", "Wohnhaus");
        var generation = await meters.CreateAsync([customer.Id], "AT-GENERATION", "generation", null);
        var deletedMeter = await meters.CreateAsync([customer.Id], "AT-DELETED", "consumption", null);
        await meters.CreateAsync([other.Id], "AT-OTHER", "consumption", null);
        var community = await communities.CreateAsync("EEG Sonne");
        var second = await communities.CreateAsync("EEG Wind");
        var deletedCommunity = await communities.CreateAsync("EEG Gelöscht");
        var removedCommunity = await communities.CreateAsync("EEG Entfernt");
        await communities.SetParticipationAsync(community.Id, shared.Id, 37.5m);
        await communities.SetParticipationAsync(second.Id, shared.Id, 100m);
        await communities.SetParticipationAsync(deletedCommunity.Id, shared.Id, 20m);
        await communities.SetParticipationAsync(removedCommunity.Id, shared.Id, 20m);
        context.ChangeTracker.Clear();
        await communities.DeleteAsync(deletedCommunity.Id);
        context.ChangeTracker.Clear();
        await communities.RemoveParticipationAsync(removedCommunity.Id, shared.Id);
        context.ChangeTracker.Clear();
        await meters.DeleteAsync(deletedMeter.Id);
        context.ChangeTracker.Clear();

        var result = Assert.IsType<FileContentResult>(await CreateController(customers).Get("K-1", CancellationToken.None));
        using var pdf = PdfDocument.Open(result.FileContents);
        var text = Assert.Single(pdf.GetPages()).Text;
        Assert.Contains("AT-SHARED", text);
        Assert.Contains("AT-GENERATION", text);
        Assert.Contains("Bezug", text);
        Assert.Contains("Einspeisung", text);
        Assert.Contains("Wohnhaus", text);
        Assert.Contains("EEG Sonne (37,5 % Teilnahme)", text);
        Assert.Contains("EEG Wind (100 % Teilnahme)", text);
        Assert.Contains("keine zugeordnet", text);
        Assert.DoesNotContain("AT-OTHER", text);
        Assert.DoesNotContain("AT-DELETED", text);
        Assert.DoesNotContain("EEG Gelöscht", text);
        Assert.DoesNotContain("EEG Entfernt", text);
    }

    [Fact]
    public async Task Get_FitsManyMetersOnOnePageWithoutClippingOrLosingEntries()
    {
        await using var context = CreateDbContext();
        var unit = new UnitOfWork(context);
        var customers = new CustomerService(unit);
        var meters = new MeteringPointService(unit);
        var customer = await customers.CreateAsync("K-MANY", "Kunde", null);
        for (var i = 0; i < 30; i++)
        {
            await meters.CreateAsync([customer.Id], $"AT-METER-{i:00}", "consumption", new string('W', 200));
        }
        context.ChangeTracker.Clear();

        var result = Assert.IsType<FileContentResult>(await CreateController(customers).Get("K-MANY", CancellationToken.None));
        using var pdf = PdfDocument.Open(result.FileContents);
        var page = Assert.Single(pdf.GetPages());
        var text = page.Text;
        for (var i = 0; i < 30; i++)
        {
            Assert.Contains($"AT-METER-{i:00}", text);
        }
        var image = Assert.Single(page.GetImages());
        Assert.InRange(Math.Abs((image.Bounds.Left + image.Bounds.Right) / 2 - page.Width / 2), 0, 0.1);
        Assert.All(page.Letters, letter =>
        {
            Assert.InRange(letter.GlyphRectangle.Left, 0, page.Width);
            Assert.InRange(letter.GlyphRectangle.Right, 0, page.Width);
            Assert.InRange(letter.GlyphRectangle.Bottom, 0, page.Height);
            Assert.InRange(letter.GlyphRectangle.Top, 0, page.Height);
        });
    }

    private static RegistrationDocumentController CreateController(CustomerService service)
    {
        return new RegistrationDocumentController(service, new RegistrationDocumentService())
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"registration-document-{Guid.NewGuid()}")
            .Options;
        return new ApplicationDbContext(options);
    }
}
