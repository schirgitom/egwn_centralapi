namespace EGWNCentral.Core.Abstractions;

public interface ICustomerSnapshotService
{
    Task<Guid?> EnqueueCreatedAsync(string customerNumber, CancellationToken cancellationToken = default);

    Task<int> EnqueueAllCreatedAsync(CancellationToken cancellationToken = default);
}
