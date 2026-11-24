using EGWNCentral.Core.Domain.Entities;

namespace EGWNCentral.Core.Abstractions;

public interface ICustomerService
{
    Task<IReadOnlyCollection<Customer>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<Customer?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Customer?> GetByNumberAsync(string customerNumber, CancellationToken cancellationToken = default);

    Task<Customer> CreateAsync(string customerNumber, string name, string? email, CancellationToken cancellationToken = default);

    Task<Customer?> UpdateAsync(Guid id, string name, string? email, CancellationToken cancellationToken = default);

    Task<string?> RotateRegistrationCodeAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
