using EGWNCentral.Api.Services;
using EGWNCentral.Core.Abstractions;
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
        return File(pdf, "application/pdf", "registrierung.pdf");
    }
}
