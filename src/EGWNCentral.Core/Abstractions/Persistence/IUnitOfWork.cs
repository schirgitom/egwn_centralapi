using EGWNCentral.Core.Domain.Entities;

namespace EGWNCentral.Core.Abstractions.Persistence;

public interface IUnitOfWork
{
    IRepository<Customer> Customers { get; }

    IRepository<MeteringPoint> MeteringPoints { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
