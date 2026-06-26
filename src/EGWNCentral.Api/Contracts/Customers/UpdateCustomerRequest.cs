namespace EGWNCentral.Api.Contracts.Customers;

public sealed class UpdateCustomerRequest
{
    public string Name { get; set; } = string.Empty;

    public string? Email { get; set; }
}
