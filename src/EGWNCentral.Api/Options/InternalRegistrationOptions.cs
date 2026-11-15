namespace EGWNCentral.Api.Options;

public sealed class InternalRegistrationOptions
{
    public const string SectionName = "InternalRegistration";

    public string Token { get; set; } = string.Empty;

    public string TicketSigningKey { get; set; } = string.Empty;
}
