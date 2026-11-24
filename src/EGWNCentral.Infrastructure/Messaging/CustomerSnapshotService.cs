using EGWNCentral.Core.Abstractions;
using EGWNCentral.Core.Domain.Entities;
using EGWNCentral.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EGWNCentral.Infrastructure.Messaging;

public sealed class CustomerSnapshotService(ApplicationDbContext dbContext) : ICustomerSnapshotService
{
    public async Task<Guid?> EnqueueCreatedAsync(string customerNumber, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(customerNumber);

        var customer = await CustomerSnapshotBuilder.WithRelationships(dbContext.Customers)
            .SingleOrDefaultAsync(x => x.CustomerNumber == customerNumber.Trim(), cancellationToken);
        if (customer is null)
        {
            return null;
        }

        var message = CreateMessage(customer);
        dbContext.OutboxMessages.Add(message);
        await dbContext.SaveChangesAsync(cancellationToken);
        return message.Id;
    }

    public async Task<int> EnqueueAllCreatedAsync(CancellationToken cancellationToken = default)
    {
        var customers = await CustomerSnapshotBuilder.WithRelationships(dbContext.Customers)
            .ToListAsync(cancellationToken);
        foreach (var customer in customers)
        {
            cancellationToken.ThrowIfCancellationRequested();
            dbContext.OutboxMessages.Add(CreateMessage(customer));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return customers.Count;
    }

    private OutboxMessage CreateMessage(Customer customer)
    {
        var message = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            OccurredAtUtc = DateTimeOffset.UtcNow
        };
        message.Payload = CustomerSnapshotBuilder.Serialize(dbContext, customer, message, "customer.created");
        return message;
    }
}
