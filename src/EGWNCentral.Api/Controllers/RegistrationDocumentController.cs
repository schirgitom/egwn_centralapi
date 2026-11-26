using System.IO.Compression;
using System.Text;
using EGWNCentral.Api.Services;
using EGWNCentral.Core.Abstractions;
using EGWNCentral.Core.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EGWNCentral.Api.Controllers;

[ApiController]
[Route("api/customers/by-number")]
[Authorize]
public sealed class RegistrationDocumentController(
    ICustomerService customerService,
    RegistrationDocumentService documentService) : ControllerBase
{
    [HttpGet("{customerNumber}/registration-document")]
    [Produces("application/pdf")]
    public async Task<IActionResult> Get(string customerNumber, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "private, no-store";
        Response.Headers.Pragma = "no-cache";

        if (string.IsNullOrWhiteSpace(customerNumber))
        {
            return BadRequest("Customer number cannot be empty.");
        }

        var customer = await customerService.GetByNumberAsync(customerNumber, cancellationToken);
        if (customer is null)
        {
            return NotFound();
        }

        var pdf = documentService.Create(customer);
        return File(pdf, "application/pdf", FileName(customer));
    }

    [HttpGet("/api/customers/registration-documents")]
    [Produces("application/zip")]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "private, no-store";
        Response.Headers.Pragma = "no-cache";

        var customers = await customerService.GetAllWithRelationshipsAsync(cancellationToken);
        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true, Encoding.UTF8))
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var customer in customers)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var name = FileName(customer);
                var baseName = name[..^4];
                for (var suffix = 2; !names.Add(name); suffix++)
                {
                    name = $"{baseName} ({suffix}).pdf";
                }

                var entry = archive.CreateEntry(name);
                await using var stream = entry.Open();
                await stream.WriteAsync(documentService.Create(customer), cancellationToken);
            }
        }

        return File(output.ToArray(), "application/zip", "registrierungsdokumente.zip");
    }

    private static string FileName(Customer customer)
    {
        const string invalid = "<>:\"/\\|?*";
        var value = $"{customer.CustomerNumber} - {customer.Name}";
        var builder = new StringBuilder();
        var bytes = 0;
        foreach (var rune in value.EnumerateRunes())
        {
            var safe = Rune.IsControl(rune) || (rune.IsAscii && invalid.Contains((char)rune.Value))
                ? new Rune('_')
                : rune;
            if (bytes + safe.Utf8SequenceLength > 220)
            {
                break;
            }
            builder.Append(safe.ToString());
            bytes += safe.Utf8SequenceLength;
        }
        return builder.ToString().TrimEnd(' ', '.') + ".pdf";
    }
}
