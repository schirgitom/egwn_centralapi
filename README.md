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
- `InternalRegistration:Token`, `InternalRegistration:TicketSigningKey`
  (nur ueber Secrets/Umgebungsvariablen setzen; beide mindestens 32 UTF-8-Bytes)
- `Consul:Enabled`, `Consul:Address`, `Consul:Key`
- `Seq:Url`
- `RabbitMQ:Enabled`, `RabbitMQ:HostName`, `RabbitMQ:Port`, `RabbitMQ:VirtualHost`
- `RabbitMQ:UserName`, `RabbitMQ:Password`, `RabbitMQ:UseTls`
- `RabbitMQ:Exchange`, `RabbitMQ:RoutingKey`, `RabbitMQ:Queue`

In der Entwicklungsumgebung bietet Swagger UI unter `/swagger` den Button
**Authorize**: Fuer geschuetzte API-Endpunkte den `accessToken` aus
`POST /api/auth/token` bei **Bearer** ohne das Praefix `Bearer` eingeben.
Swagger sendet ihn automatisch im Authorization-Header. Die internen
Keycloak-Endpunkte verwenden stattdessen **InternalBearer** mit dem separat
konfigurierten internen Token.

## Kundenstand und RabbitMQ

Bei jeder Kundenanlage und relevanten Aenderung ueber EF Core werden die
Fachdaten und ein vollstaendiger Kunden-Snapshot in derselben Datenbanktransaktion
gespeichert. Dazu gehoeren Kundenstammdaten, alle aktiven zugeordneten Zaehlpunkte
und deren aktive Energiegemeinschaften mit Teilnahmeprozenten. Ein
Hintergrunddienst versendet ausstehende Events aus `CustomerCreatedOutbox`.
RabbitMQ muss bei der Kundenanlage nicht erreichbar sein. Nach einer
Broker-Bestaetigung wird der Outbox-Eintrag entfernt; Fehler werden protokolliert
und nach `RabbitMQ:DispatchIntervalSeconds` (Standard: 5 Sekunden) erneut versucht.
Pro Durchlauf werden maximal `RabbitMQ:BatchSize` (Standard: 100) Events versendet.

Der Versand ist standardmaessig deaktiviert. Fuer die Inbetriebnahme zuerst die
Migration anwenden und dann `RabbitMQ:Enabled=true` sowie Broker-Zugangsdaten
konfigurieren. Auch bei deaktiviertem Versand werden neue Events gespeichert;
nach der Aktivierung und einem Neustart wird der Rueckstand versendet.
Passwoerter ueber Secrets, Umgebungsvariablen (z. B. `RabbitMQ__Password`) oder
die vorhandene Consul-Konfiguration bereitstellen, nicht ins Repository schreiben.
Fuer TLS `UseTls=true` und den TLS-Port des Brokers (typisch 5671) setzen.

Mit `POST /api/customers/by-number/{customerNumber}/grafana` (Bearer-Token erforderlich)
laesst sich fuer einen aktiven Bestandskunden ein vollstaendiger `customer.created`-Snapshot
erneut in die Outbox einreihen, z. B. fuer Kundennummer `232`. Kundennummern in der URL
gegebenenfalls URL-kodieren. Die Antwort `202 Accepted` mit `{"eventId":"<GUID>"}`
bestaetigt nur die dauerhafte Speicherung in der Outbox, nicht die Zustellung an
RabbitMQ oder Verarbeitung durch Grafana. Jeder Aufruf erzeugt eine neue Event-ID;
unbekannte oder geloeschte Kunden ergeben `404`. Auch bei deaktiviertem RabbitMQ
bleibt das Event bis zur Aktivierung ausstehend. Die Nachricht entspricht dem
aktuellen Snapshot-Schema und enthaelt auch `registrationCode` und `email`.

Mit `POST /api/customers/grafana` (Bearer-Token erforderlich) wird fuer jeden
aktiven Kunden ein solcher Snapshot in die Outbox eingereiht. Die Antwort
`202 Accepted` mit `{"count":2}` gibt die Anzahl der eingereihten Nachrichten
an; bei keinen aktiven Kunden ist der Wert `0`. Geloeschte Kunden werden nicht
beruecksichtigt. Die Nachrichten werden gemeinsam gespeichert, aber anschliessend
unabhaengig voneinander versendet und verarbeitet. Die Antwort bestaetigt weder
die Zustellung noch die Anlage in Grafana. Jeder erneute Aufruf erzeugt neue
Events; der Grafana-Service muss wiederholte Kundenereignisse idempotent verarbeiten.

Standardvertrag fuer den separat betriebenen Grafana-Microservice:

| Eigenschaft | Wert |
|---|---|
| Exchange | `egwncentral.events` (Topic, durable) |
| Routing-Key | `customer.created.v1` (bestehendes Binding bleibt erhalten) |
| AMQP-Type | `customer.created.v1`, `customer.updated.v1` oder `customer.deleted.v1` |
| Queue | `grafana.customer-created.v1` (durable, nicht exklusiv) |
| AMQP-MessageId | `eventId` |
| Content-Type / Encoding | `application/json` / `utf-8` |

Die API deklariert Exchange, Queue und Binding vor dem Versand. Dadurch werden
persistente Nachrichten auch ohne laufenden Grafana-Service aufbewahrt.
Der Broker-Benutzer benoetigt entsprechende Configure-, Write- und Read-Rechte.
Bereits vorhandene Topologien muessen dieselben Eigenschaften verwenden.
Der Versand nutzt Publisher Confirms und `mandatory`, damit abgelehnte oder
nicht routbare Nachrichten in der Outbox bleiben.

```json
{
  "eventId": "c1fd7cf7-eaf2-46de-a340-7dc50caf600b",
  "eventType": "customer.created",
  "schemaVersion": 1,
  "occurredAtUtc": "2026-11-09T17:00:00+00:00",
  "customer": {
    "id": "c1fd7cf7-eaf2-46de-a340-7dc50caf600b",
    "customerNumber": "K-100",
    "registrationCode": "BK7X39",
    "name": "Musterkunde",
    "email": "kunde@example.com",
    "isDeleted": false,
    "meteringPoints": [
      {
        "id": "09c7e9f9-eb4d-475f-942e-ad48faea4633",
        "meteringPointCode": "AT001000000000000000000000000001",
        "energyDirection": "consumption",
        "description": "Haus"
      }
    ],
    "energyCommunities": [
      {
        "id": "f199cb75-efca-4c79-8ac8-020143986ed8",
        "name": "EEG Muster",
        "source": "EDA",
        "sourceId": "external-123",
        "meteringPoints": [
          {
            "id": "09c7e9f9-eb4d-475f-942e-ad48faea4633",
            "meteringPointCode": "AT001000000000000000000000000001",
            "energyDirection": "consumption",
            "description": "Haus",
            "participationPercentage": 75
          }
        ]
      }
    ]
  }
}
```

`email`, `description`, `source` und `sourceId` sind nullable. Ein Kunde kann
mehreren Gemeinschaften angehoeren. `customer.meteringPoints` enthaelt auch
Zaehlpunkte ohne Gemeinschaft; `customer.energyCommunities[].meteringPoints`
enthaelt ausschliesslich die Zaehlpunkte dieses Kunden in dieser Gemeinschaft.
Ein Zaehlpunkt kann in mehreren Gemeinschaften mit jeweils eigenem Teilnahmegrad
vorkommen. Noch nicht vorhandene Zuordnungen werden als leere Arrays gesendet.

`registrationCode` ist ein einmalig zufaellig erzeugter Code mit genau sechs
Zeichen aus `A-Z` und `1-9`, ohne `O` und `0`. Er wird beim Kunden gespeichert, bleibt
bei Aenderungen und Loeschungen erhalten und wird in jedem neuen Snapshot
mitgesendet. Ein eindeutiger Datenbankindex gilt auch fuer geloeschte Kunden.
Die Migration `AddCustomerRegistrationCode` befuellt bestehende Kunden
einschliesslich Seed-Datensaetzen mit zufaelligen Codes. Sie erzeugt keine
nachtraeglichen RabbitMQ-Events; Bestandskunden senden den Code bei ihrer
naechsten relevanten Aenderung. Bereits gespeicherte Outbox-Nachrichten bleiben
unveraendert und koennen den Code noch nicht enthalten.
Der Code wird nicht aus Name, Kundennummer oder Kunden-ID abgeleitet. Die
geschuetzte Route `POST /api/customers/{id}/registration-code/rotate` erzeugt
einen neuen Code und gibt ihn einmalig in der Antwort zurueck; ausstehende
Kunden-Snapshots werden mit dem neuen Code aktualisiert. Codes nicht
protokollieren; den Zugriff auf Datenbank und RabbitMQ auf berechtigte Dienste
beschraenken.

Mit `GET /api/customers/by-number/{customerNumber}/registration-document`
(Bearer-Token erforderlich) kann fuer einen aktiven Kunden ein PDF mit Logo,
Name, Kundennummer und aktuellem Registrierungscode heruntergeladen werden.
Zugeordnete aktive Zaehlpunkte werden mit Richtung (Bezug oder Einspeisung),
gegebenenfalls Bezeichnung sowie aktiven Energiegemeinschaften und Teilnahmegrad
auf derselben Seite aufgefuehrt. Bei vielen Eintraegen wird der gesamte Inhalt
verkleinert und kann dadurch schwer lesbar werden.
Die Kundennummer gegebenenfalls URL-kodieren. Unbekannte oder geloeschte Kunden
ergeben `404`. Der Abruf erstellt oder rotiert keinen Code; nach einer Rotation
ist ein zuvor heruntergeladenes Dokument nicht mehr aktuell. Die Antwort enthaelt
`Cache-Control: private, no-store`. Das PDF und der Code sind vertraulich zu
behandeln. Das eingebettete Lato-Schriftbild steht unter der SIL Open Font
License; der Lizenztext liegt in `src\EGWNCentral.Api\Assets\Lato-OFL.txt`.
Die Folgemigration `ShortenCustomerRegistrationCode` ersetzt bisherige lange
oder ungueltige Codes durch eindeutige sechsstellige Codes. Bereits versandte
Codes verlieren dadurch ihre Gueltigkeit. Ausstehende Outbox-Payloads werden
auf den neuen Code aktualisiert, ihre Event-ID bleibt erhalten. Bereits
zugestellte Nachrichten werden nicht automatisch erneut versendet.
Die kurze Kennung darf nicht als alleiniger Authentifizierungsnachweis dienen;
ein Registrierungsdienst benoetigt insbesondere Rate-Limiting.
Beim Rollback der Kurzcode-Migration bleiben die kurzen Codes erhalten;
die urspruenglichen langen Codes werden nicht wiederhergestellt.

## Keycloak-Registrierung

Fuer die drei internen Endpunkte muss `InternalRegistration:Token` als
`EGWN_INTERNAL_TOKEN` an den Keycloak-Provider und als
`InternalRegistration__Token` an die CentralAPI uebergeben werden. Beide
Variablen muessen denselben zufaelligen, mindestens 32 Byte langen Wert
enthalten. `InternalRegistration:TicketSigningKey` ist ein davon unabhaengiger,
mindestens 32 Byte langer API-Schluessel und bleibt ausschliesslich bei der
CentralAPI. Keine dieser Variablen in Realm-Exports, Images oder Browser-Code
ablegen.

Keycloak ruft mit `Authorization: Bearer <EGWN_INTERNAL_TOKEN>` diese JSON-POST-
Routen auf:

| Route | Request | HTTP-200-Antwort |
|---|---|---|
| `/internal/registration/resolve` | `{"code":"ABC123"}` | `{"ticket":"...","organization":"egwn-<customer-id-ohne-bindestriche>"}` |
| `/internal/registration/assign` | `{"ticket":"...","userId":"<immutable-keycloak-id>"}` | `{"customerId":"<GUID>","organization":"egwn-<customer-id-ohne-bindestriche>"}` |
| `/internal/registration/membership` | `{"userId":"<immutable-keycloak-id>"}` | `{"customerId":"<GUID>","organization":"egwn-<customer-id-ohne-bindestriche>"}` |

Fehlerhafte Codes/Tickets, fehlende oder geloeschte Kunden und fehlende
Mitgliedschaften ergeben HTTP 400. Tickets sind HMAC-signiert, enthalten nur
einen Hash des Registrierungscodes und verfallen nach spaetestens fuenf Minuten.
Bei der Zuordnung wird der aktuelle Kundenstatus und Code erneut geprueft.
Keycloak-User-IDs werden eindeutig einem Kunden zugeordnet: Wiederholte
Zuordnungen zum selben Kunden sind idempotent, eine Umzuordnung wird abgelehnt.
Die Organisation ist stabil `egwn-` plus Kunden-GUID ohne Bindestriche.
Migrationen vor dem Start der API anwenden.

`eventType` ist bei Anlage `customer.created`, bei relevanten Aenderungen
`customer.updated` und bei Kundenloeschung `customer.deleted`. Eine neue Nachricht
wird bei Aenderungen an Kundenstammdaten, Zaehlpunkten, Kundenzuordnungen,
Gemeinschaftsstammdaten und Gemeinschaftsteilnahmen erzeugt, einschliesslich
Loeschungen. Entfernte Zuordnungen fehlen im folgenden vollstaendigen Snapshot.
Bei Kundenloeschung ist `isDeleted=true`, beide Listen sind leer.
Auch die bisherigen Besitzer eines neu zugeordneten Zaehlpunkts werden informiert.
Gemeinsam genutzte Zaehlpunkte werden fuer jeden betroffenen Kunden abgebildet;
fremde Zaehlpunkte derselben Gemeinschaft werden nicht mitgesendet.

Der Routing-Key und die Queue bleiben auch fuer Aenderungen und Loeschungen
unveraendert; der Consumer soll nicht nur `customer.created` verarbeiten.
`schemaVersion=1` wird um zusaetzliche Felder erweitert. Bereits zuvor gespeicherte
Outbox-Nachrichten bleiben unveraendert und koennen diese Felder noch nicht enthalten;
fehlende Listen in solchen alten Nachrichten sind kein Loeschsignal.
Bestandskunden und model-managed Seed-Daten aus Migrationen werden nicht
nachtraeglich versendet, erhalten aber bei der naechsten relevanten Aenderung
einen vollstaendigen Snapshot.

Bei Anlage entspricht `eventId` der Kunden-ID. Jede spaetere Aenderung hat eine
eigene Event-ID; bei Wiederholungen desselben Events bleibt diese unveraendert.
Zustellung erfolgt
**mindestens einmal**, nicht exakt einmal: Ein Absturz nach Broker-Bestaetigung
oder mehrere API-Instanzen koennen doppelte Zustellungen verursachen. Der
Grafana-Service muss anhand von `eventId` deduplizieren und anhand der Kunden-ID idempotent
arbeiten und erst nach erfolgreicher Verarbeitung bestaetigen (manuelles ACK).
Die Nachricht ist ein vollstaendiger Ersatzstand, kein Delta. Bei parallelen
API-Instanzen oder Consumern ist keine strikte Zustellreihenfolge garantiert.
Die API legt keine Grafana-Organisationen oder Dashboards selbst an.

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

Energiegemeinschaften koennen optional eine Herkunft `source` (z. B. `EDA` oder
`EEGFaktura`) und eine zugehoerige externe Kennung `sourceId` enthalten. Beide
Felder koennen beim Erstellen oder Aktualisieren mitgegeben werden und werden
auch in den Antworten zurueckgegeben. Die Migration uebernimmt bestehende Werte
aus der Datenbankspalte `Quelle` in `SourceId`; die neue `Source` bleibt dabei
unbekannt (`NULL`). Das bisherige API-Feld `source` hat damit eine neue Bedeutung:
Clients muessen ihre bisher dort uebermittelte Kennung kuenftig als `sourceId` senden.
Beim Rollback der Migration bleibt die externe Kennung in `Quelle` erhalten; die
neue Herkunft `Source` geht dabei verloren.

`GET /api/energy-communities/by-source?source=EDA` liefert die aktiven
Energiegemeinschaften dieser Herkunft mit denselben Details wie die Gesamtliste.
Jede Teilnahme enthaelt die Energierichtung des Zaehlpunkts als `energyDirection`
(`generation` oder `consumption`); dies gilt auch fuer die Detailansicht und die
Antwort beim Setzen einer Teilnahme.
Der Vergleich ist exakt und unterscheidet Gross- und Kleinschreibung; fuehrende
und nachfolgende Leerzeichen werden entfernt. Eine fehlende oder leere Herkunft
ergibt HTTP 400, eine unbekannte Herkunft eine leere Liste.
