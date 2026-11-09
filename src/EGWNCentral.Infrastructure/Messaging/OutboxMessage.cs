namespace EGWNCentral.Infrastructure.Messaging;

public sealed class OutboxMessage
{
    public Guid Id { get; set; }

    public DateTimeOffset OccurredAtUtc { get; set; }

    public string Payload { get; set; } = string.Empty;
}
