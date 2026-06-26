# EGWNCentral

EGWNCentral ist eine .NET Web API zur Verwaltung einer Energiegemeinschaft mit Fokus auf **Kunden** und **Zaehlpunkte**.

## Technologie-Stack

- ASP.NET Core Web API (.NET 10)
- EF Core + PostgreSQL
- JWT Authentication
- Serilog + Seq
- Consul als Konfigurationsquelle (Load on startup)
- xUnit fuer Unit Tests

## Projektstruktur

- `src\EGWNCentral.Api` - HTTP API, Auth, Konfiguration, Logging
- `src\EGWNCentral.Core` - Domain-Modelle und Service-Abstraktionen
- `src\EGWNCentral.Infrastructure` - EF Core, Persistence, Service-Implementierungen
- `tests\EGWNCentral.Tests` - Unit Tests

## Konfiguration

Wichtige Einstellungen in `src\EGWNCentral.Api\appsettings.json`:

- `ConnectionStrings:DefaultConnection`
- `Jwt:Issuer`, `Jwt:Audience`, `Jwt:SigningKey`, `Jwt:ExpirationMinutes`
- `Auth:Username`, `Auth:Password` (V1 Login fuer Token-Endpoint)
- `Consul:Enabled`, `Consul:Address`, `Consul:Key`
- `Seq:Url`

## Lokaler Start

1. PostgreSQL bereitstellen und `ConnectionStrings:DefaultConnection` anpassen.
2. Migration anwenden:

   `dotnet ef database update --project .\src\EGWNCentral.Infrastructure\EGWNCentral.Infrastructure.csproj --startup-project .\src\EGWNCentral.Api\EGWNCentral.Api.csproj`

3. API starten:

   `dotnet run --project .\src\EGWNCentral.Api\EGWNCentral.Api.csproj`

## Authentifizierung

1. Token holen:

   `POST /api/auth/token`

   Beispiel-Body:
   `{"username":"admin","password":"admin-change-me"}`

2. `accessToken` als Bearer Token fuer geschuetzte Endpunkte verwenden:

- `/api/customers`
- `/api/metering-points`
