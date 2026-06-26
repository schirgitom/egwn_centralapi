namespace EGWNCentral.Api.Contracts.Customers;

public sealed class CreateCustomerRequest
{
    public string CustomerNumber { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Email { get; set; }
}
