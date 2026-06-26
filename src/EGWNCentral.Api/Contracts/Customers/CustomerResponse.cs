namespace EGWNCentral.Api.Contracts.Customers;

public sealed record CustomerResponse(
    Guid Id,
    string CustomerNumber,
    string Name,
    string? Email,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc);
