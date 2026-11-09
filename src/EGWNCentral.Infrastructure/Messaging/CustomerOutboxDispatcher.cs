using EGWNCentral.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EGWNCentral.Infrastructure.Messaging;

public sealed class CustomerOutboxDispatcher(
    ApplicationDbContext dbContext,
    ICustomerEventPublisher publisher,
    IOptions<RabbitMqOptions> options)
{
    public async Task DispatchAsync(CancellationToken cancellationToken)
    {
        var messages = await dbContext.OutboxMessages
            .OrderBy(x => x.OccurredAtUtc)
            .ThenBy(x => x.Id)
            .Take(options.Value.BatchSize)
            .ToListAsync(cancellationToken);

        foreach (var message in messages)
        {
            await publisher.PublishAsync(message, cancellationToken);
            dbContext.OutboxMessages.Remove(message);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
