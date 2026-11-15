using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using EGWNCentral.Api.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Options;

namespace EGWNCentral.Api.Services;

public sealed class RegistrationTicketService(IOptions<InternalRegistrationOptions> options)
{
    private const int TicketLifetimeSeconds = 5 * 60;

    public string Create(Guid customerId, string registrationCode)
    {
        var issuedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var payload = new TicketPayload(
            customerId,
            HashRegistrationCode(registrationCode),
            issuedAt,
            issuedAt + TicketLifetimeSeconds);
        var encodedPayload = Base64UrlEncoder.Encode(JsonSerializer.SerializeToUtf8Bytes(payload));
        var signature = HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(options.Value.TicketSigningKey),
            Encoding.ASCII.GetBytes(encodedPayload));
        return $"{encodedPayload}.{Base64UrlEncoder.Encode(signature)}";
    }

    public bool TryValidate(string? ticket, out Guid customerId, out string registrationCodeHash)
    {
        customerId = Guid.Empty;
        registrationCodeHash = string.Empty;
        if (string.IsNullOrWhiteSpace(ticket) || ticket.Length > 8192)
        {
            return false;
        }

        var separator = ticket.IndexOf('.');
        if (separator <= 0 || separator != ticket.LastIndexOf('.'))
        {
            return false;
        }

        try
        {
            var encodedPayload = ticket[..separator];
            var suppliedSignature = Base64UrlEncoder.DecodeBytes(ticket[(separator + 1)..]);
            var expectedSignature = HMACSHA256.HashData(
                Encoding.UTF8.GetBytes(options.Value.TicketSigningKey),
                Encoding.ASCII.GetBytes(encodedPayload));
            if (!CryptographicOperations.FixedTimeEquals(expectedSignature, suppliedSignature))
            {
                return false;
            }

            var payload = JsonSerializer.Deserialize<TicketPayload>(Base64UrlEncoder.DecodeBytes(encodedPayload));
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (payload is null || payload.CustomerId == Guid.Empty ||
                payload.RegistrationCodeHash is not { Length: 43 } ||
                payload.IssuedAtUnixSeconds > now ||
                payload.ExpiresAtUnixSeconds <= now ||
                payload.ExpiresAtUnixSeconds <= payload.IssuedAtUnixSeconds ||
                payload.ExpiresAtUnixSeconds - payload.IssuedAtUnixSeconds > TicketLifetimeSeconds)
            {
                return false;
            }

            customerId = payload.CustomerId;
            registrationCodeHash = payload.RegistrationCodeHash;
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
        catch (JsonException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    public static string HashRegistrationCode(string registrationCode)
        => Base64UrlEncoder.Encode(SHA256.HashData(Encoding.UTF8.GetBytes(registrationCode)));

    private sealed record TicketPayload(
        Guid CustomerId,
        string RegistrationCodeHash,
        long IssuedAtUnixSeconds,
        long ExpiresAtUnixSeconds);
}
