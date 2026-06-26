using EGWNCentral.Core.Abstractions;
using EGWNCentral.Core.Abstractions.Persistence;
using EGWNCentral.Core.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EGWNCentral.Infrastructure.Services;

public sealed class CustomerService(IUnitOfWork unitOfWork) : ICustomerService
{
    public async Task<IReadOnlyCollection<Customer>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await unitOfWork.Customers.Query(asNoTracking: true)
            .OrderBy(x => x.CustomerNumber)
            .ToListAsync(cancellationToken);
    }

    public Task<Customer?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return unitOfWork.Customers.Query(asNoTracking: true)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<Customer> CreateAsync(string customerNumber, string name, string? email, CancellationToken cancellationToken = default)
    {
        var normalizedCustomerNumber = Normalize(customerNumber);

        var exists = await unitOfWork.Customers.Query(asNoTracking: true)
            .AnyAsync(x => x.CustomerNumber == normalizedCustomerNumber, cancellationToken);

        if (exists)
        {
            throw new InvalidOperationException($"Customer number '{normalizedCustomerNumber}' already exists.");
        }

        var customer = new Customer
        {
            CustomerNumber = normalizedCustomerNumber,
            Name = Normalize(name),
            Email = NormalizeNullable(email)
        };

        await unitOfWork.Customers.AddAsync(customer, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return customer;
    }

    public async Task<Customer?> UpdateAsync(Guid id, string name, string? email, CancellationToken cancellationToken = default)
    {
        var customer = await unitOfWork.Customers.GetByIdAsync(id, cancellationToken);
        if (customer is null)
        {
            return null;
        }

        customer.Name = Normalize(name);
        customer.Email = NormalizeNullable(email);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return customer;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var customer = await unitOfWork.Customers.GetByIdAsync(id, cancellationToken);
        if (customer is null)
        {
            return false;
        }

        var meteringPoints = await unitOfWork.MeteringPoints.Query()
            .Where(x => x.CustomerId == id)
            .ToListAsync(cancellationToken);

        foreach (var meteringPoint in meteringPoints)
        {
            unitOfWork.MeteringPoints.Remove(meteringPoint);
        }

        unitOfWork.Customers.Remove(customer);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static string Normalize(string value)
    {
        var normalized = value.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new ArgumentException("Value cannot be empty.", nameof(value));
        }

        return normalized;
    }

    private static string? NormalizeNullable(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
