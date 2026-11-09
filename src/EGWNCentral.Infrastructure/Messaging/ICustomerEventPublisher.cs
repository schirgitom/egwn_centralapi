namespace EGWNCentral.Infrastructure.Messaging;

public interface ICustomerEventPublisher
{
    Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken);
}
