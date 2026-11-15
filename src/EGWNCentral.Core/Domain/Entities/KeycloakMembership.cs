namespace EGWNCentral.Core.Domain.Entities;

public sealed class KeycloakMembership
{
    public string KeycloakUserId { get; set; } = string.Empty;

    public Guid CustomerId { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}
