using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace EGWNCentral.Infrastructure.Messaging;

public sealed class RabbitMqCustomerEventPublisher(IOptions<RabbitMqOptions> options)
    : ICustomerEventPublisher, IAsyncDisposable
{
    private IConnection? _connection;
    private IChannel? _channel;

    public async Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        if (_channel is null)
        {
            var factory = new ConnectionFactory
            {
                HostName = settings.HostName,
                Port = settings.Port,
                VirtualHost = settings.VirtualHost,
                UserName = settings.UserName,
                Password = settings.Password,
                AutomaticRecoveryEnabled = false,
                Ssl = new SslOption
                {
                    Enabled = settings.UseTls,
                    ServerName = settings.HostName
                }
            };
            _connection = await factory.CreateConnectionAsync(cancellationToken);
            _channel = await _connection.CreateChannelAsync(
                new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true),
                cancellationToken);
            await _channel.ExchangeDeclareAsync(settings.Exchange, ExchangeType.Topic, durable: true,
                autoDelete: false, cancellationToken: cancellationToken);
            await _channel.QueueDeclareAsync(settings.Queue, durable: true, exclusive: false,
                autoDelete: false, cancellationToken: cancellationToken);
            await _channel.QueueBindAsync(settings.Queue, settings.Exchange, settings.RoutingKey,
                cancellationToken: cancellationToken);
        }

        using var payload = JsonDocument.Parse(message.Payload);
        var eventType = payload.RootElement.GetProperty("eventType").GetString();
        var schemaVersion = payload.RootElement.GetProperty("schemaVersion").GetInt32();
        var properties = new BasicProperties
        {
            MessageId = message.Id.ToString(),
            Type = $"{eventType}.v{schemaVersion}",
            ContentType = "application/json",
            ContentEncoding = "utf-8",
            Persistent = true,
            Timestamp = new AmqpTimestamp(message.OccurredAtUtc.ToUnixTimeSeconds())
        };
        await _channel.BasicPublishAsync(settings.Exchange, settings.RoutingKey, mandatory: true,
            basicProperties: properties, body: Encoding.UTF8.GetBytes(message.Payload),
            cancellationToken: cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel is not null)
        {
            await _channel.DisposeAsync();
        }
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
        }
    }
}
