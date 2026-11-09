namespace EGWNCentral.Infrastructure.Messaging;

public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMQ";

    public bool Enabled { get; set; }
    public string HostName { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string VirtualHost { get; set; } = "/";
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool UseTls { get; set; }
    public string Exchange { get; set; } = "egwncentral.events";
    public string RoutingKey { get; set; } = CustomerSnapshot.RoutingKey;
    public string Queue { get; set; } = "grafana.customer-created.v1";
    public int DispatchIntervalSeconds { get; set; } = 5;
    public int BatchSize { get; set; } = 100;
}
