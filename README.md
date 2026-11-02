# EGWNCentral

EGWNCentral ist eine .NET Web API zur Verwaltung von Energiegemeinschaften mit Fokus auf **Kunden**, **Zaehlpunkte** und deren Teilnahme.

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

   Die Migration fügt die model-managed Seed-Daten für Kunden, Zählpunkte, Gemeinschaften und Teilnahmen hinzu.

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
- `/api/energy-communities`

Zaehlpunkte koennen mehreren Kunden zugeordnet werden. Die Zuordnungen werden in
`CustomerMeteringPoints` gespeichert; bestehende Zuordnungen werden bei der Migration uebernommen.

**API-Aenderung:** Zaehlpunkt-Requests und -Responses verwenden `customerIds` (Array)
anstelle von `customerId`. Beim Erstellen ist mindestens eine aktive Kunden-ID erforderlich.
`PUT /api/metering-points/{id}` ersetzt mit `customerIds` die gesamte Kundenliste;
ohne diese Angabe bleiben die Zuordnungen unveraendert. Doppelte IDs werden zusammengefasst,
leere Listen und leere GUIDs abgelehnt; unbekannte oder geloeschte Kunden ergeben HTTP 404.
Beispiel fuer das Erstellen: `{"customerIds":["<kunden-id-1>","<kunden-id-2>"],"meteringPointCode":"AT...","energyDirection":"consumption"}`.

Beim Loeschen eines Kunden werden seine Zuordnungen entfernt. Gemeinsam genutzte
Zaehlpunkte bleiben erhalten; nur Zaehlpunkte ohne weitere aktive Kunden werden
ebenfalls soft-geloescht. Gemeinschaftsteilnahmen liefern die zugeordneten Kunden als
`customers`-Array mit `id`, `customerNumber` und `name` statt einzelner Kundenfelder.

Die korrigierte Seed-Migration befuellt Zaehlpunkte und Kundenzuordnungen getrennt.
Bereits vorhandene aktive Kunden (Kundennummer), Zaehlpunkte (Zaehlpunktcode) und
Gemeinschaften (Name) werden mit ihren bestehenden IDs wiederverwendet. Stammdaten
und bestehende Teilnahmeprozente bleiben erhalten; fehlende Zuordnungen werden ergaenzt.
Mehrdeutige Gemeinschaftsnamen, widerspruechliche Seed-IDs oder eine abweichende
Energierichtung brechen die Migration mit einer expliziten Fehlermeldung ab.
Die Tabelle `__SeedCurrentCustomersAndCommunities` protokolliert ausschliesslich neu
angelegte Datensaetze und Zuordnungen fuer einen bestandsschonenden Rollback.
Fehlt dieses Protokoll bei einer zuvor mit der alten Version angewandten Migration,
wird ein automatischer Rollback aus Sicherheitsgruenden abgelehnt. Neue externe
Verweise auf Seed-Datensaetze koennen einen Rollback ebenfalls blockieren.
Nach einem fehlgeschlagenen Update wegen `MeteringPoints.CustomerId` oder bereits vorhandener Zaehlpunktcodes kann
`dotnet ef database update` mit den oben angegebenen Projektoptionen erneut ausgefuehrt werden;
ein Datenbank-Reset ist nicht erforderlich.

Die PostgreSQL-Regressionstests fuer Seed-Merge und Rollback werden mit
`EGWN_TEST_POSTGRES` aktiviert (Connection-String zu einer isolierten Testinstanz,
mit Berechtigung zum Erstellen und Loeschen von Datenbanken). Sie legen eigene
zufaellig benannte Testdatenbanken an und entfernen diese anschliessend.
Ohne diese Variable werden nur diese Datenbanktests uebersprungen.

Zaehlpunkte koennen mehreren Energiegemeinschaften zugeordnet werden. Der Teilnahmegrad wird pro Zuordnung unabhaengig von anderen Gemeinschaften auf 0 bis 100 Prozent festgelegt:

- `PUT /api/energy-communities/{communityId}/metering-points/{meteringPointId}` mit `{"participationPercentage": 75}`
- `DELETE /api/energy-communities/{communityId}/metering-points/{meteringPointId}` entfernt die Zuordnung.

Energiegemeinschaften koennen optional eine `source`-Angabe enthalten. Sie wird in der Datenbankspalte `Quelle` gespeichert und kann beim Erstellen oder Aktualisieren mitgegeben werden.
