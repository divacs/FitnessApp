# FitnessApp Backend

FitnessApp je .NET 8 backend za mali fitness booking i membership sistem. Fokus je na jednostavnom admin workflow-u, jasnim business pravilima i clean architecture pristupu bez nepotrebne kompleksnosti.

## Project Overview

Backend pokriva:

- registraciju i login korisnika
- JWT + refresh token autentikaciju
- verifikaciju i blokiranje korisnika
- treninge i rezervacije
- članarine, pakete i single sessions
- evidenciju uplata
- email notifikacije i reminder-e
- background job-ove preko Hangfire-a
- Swagger i health check endpoint

## Architecture Overview

Solution je podeljen na 5 projekata:

- `FitnessApp.API` - Web API entry point, controllers, middleware, auth setup, Swagger, app startup
- `FitnessApp.Application` - DTOs, feature interfaces, validators, mappings, application contracts
- `FitnessApp.Domain` - entities, enums, constants i core domain pravila
- `FitnessApp.Infrastructure` - EF Core, DbContext, Identity, services, email, Hangfire jobs, seed
- `FitnessApp.Tests` - unit i integration testovi

Odgovornosti su namerno odvojene:

- Domain nema EF/HTTP/email zavisnosti
- API ne sadrži business logiku
- Infrastructure implementira persistence i spoljne servise
- Application drži contracts i feature-level strukturu

## Tech Stack

- .NET 8 Web API
- ASP.NET Identity
- JWT Authentication
- Entity Framework Core
- PostgreSQL
- MailKit
- Hangfire
- FluentValidation
- Serilog
- Swagger / OpenAPI
- xUnit + FluentAssertions

## Kako Pokrenuti Projekat

1. Instaliraj:

- .NET 8 SDK
- PostgreSQL

2. Podesi connection string u `FitnessApp.API/appsettings.Development.json` ili preko user-secrets.

3. Iz root foldera restore/build:

```powershell
dotnet restore
dotnet build
```

4. Primeni migracije:

```powershell
dotnet ef database update --project FitnessApp.Infrastructure --startup-project FitnessApp.API --context AppDbContext
```

5. Pokreni API:

```powershell
dotnet run --project FitnessApp.API
```

6. U development okruženju koristi:

- Swagger UI: `https://localhost:<port>/swagger`
- Health check: `https://localhost:<port>/health`
- Hangfire dashboard: `https://localhost:<port>/hangfire`

## Connection String Setup

`appsettings.json` namerno ostavlja prazne vrednosti. Lokalni development primer već postoji u:

- `FitnessApp.API/appsettings.Development.json`

Primer PostgreSQL connection string-a:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=fitnessappdb;Username=postgres;Password=postgres"
  }
}
```

Ako koristiš drugi PostgreSQL host, port, bazu ili kredencijale, menjaj samo `DefaultConnection`.

## User-Secrets Setup

Preporuka je da osetljive vrednosti ne ostaju u `appsettings.Development.json`.

Iz root foldera:

```powershell
dotnet user-secrets init --project FitnessApp.API
```

Primer korisnih secret-a:

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=fitnessappdb;Username=postgres;Password=postgres" --project FitnessApp.API
dotnet user-secrets set "JwtSettings:Issuer" "FitnessApp.Development" --project FitnessApp.API
dotnet user-secrets set "JwtSettings:Audience" "FitnessApp.Client.Development" --project FitnessApp.API
dotnet user-secrets set "JwtSettings:Secret" "your-long-development-secret-key-here" --project FitnessApp.API
dotnet user-secrets set "EmailSettings:SmtpUsername" "your-gmail@gmail.com" --project FitnessApp.API
dotnet user-secrets set "EmailSettings:SmtpPassword" "your-gmail-app-password" --project FitnessApp.API
dotnet user-secrets set "EmailSettings:FromEmail" "your-gmail@gmail.com" --project FitnessApp.API
dotnet user-secrets set "AdminSeed:Email" "admin@example.com" --project FitnessApp.API
dotnet user-secrets set "AdminSeed:Password" "Development123" --project FitnessApp.API
dotnet user-secrets set "AdminSeed:FirstName" "Sara" --project FitnessApp.API
dotnet user-secrets set "AdminSeed:LastName" "Admin" --project FitnessApp.API
```

## Gmail App Password Setup

Email servis koristi Gmail SMTP preko:

- host: `smtp.gmail.com`
- port: `587`
- security: `StartTls`

Za Gmail nalog nije dovoljno obična lozinka. Potreban je App Password:

1. Uključi 2-Step Verification na Gmail nalogu.
2. Otvori Google Account > Security > App Passwords.
3. Kreiraj novi app password za Mail.
4. Tu vrednost postavi u:

- `EmailSettings:SmtpPassword`

Bitna polja:

- `EmailSettings:SmtpUsername`
- `EmailSettings:SmtpPassword`
- `EmailSettings:FromEmail`
- `EmailSettings:FromName`

Za ovaj projekat podrazumevani sender name je:

```text
Sara - FitnessApp
```

## Migrations Komande

Dodavanje nove migracije:

```powershell
dotnet ef migrations add <MigrationName> --project FitnessApp.Infrastructure --startup-project FitnessApp.API --context AppDbContext
```

Primena migracija:

```powershell
dotnet ef database update --project FitnessApp.Infrastructure --startup-project FitnessApp.API --context AppDbContext
```

Lista migracija:

```powershell
dotnet ef migrations list --project FitnessApp.Infrastructure --startup-project FitnessApp.API --context AppDbContext
```

Backend ne radi automatske migracije u produkciji. Migracije se pokreću ručno.

## Seed Admin Info

Pri startup-u aplikacija:

- seed-uje role `Admin` i `Korisnik`
- seed-uje default admin nalog
- ne pokušava seed ako baza nije dostupna

Admin seed dolazi iz `AdminSeed` konfiguracije.

Development podrazumevane vrednosti:

```json
"AdminSeed": {
  "Email": "admin@example.com",
  "Password": "Development123",
  "FirstName": "Sara",
  "LastName": "Admin"
}
```

Seed admin se kreira kao:

- `Verified`
- `EmailConfirmed = true`
- član `Admin` role

Preporuka je da produkcija koristi bezbedne vrednosti preko environment variables ili secret store-a.

## Auth / Refresh Token Flow

Glavni auth endpoint-i:

```text
POST /api/auth/register
POST /api/auth/login
POST /api/auth/refresh-token
POST /api/auth/logout
GET  /api/auth/me
```

Registration:

- kreira korisnika sa statusom `Unverified`
- dodeljuje rolu `Korisnik`
- ne uloguje korisnika automatski

Login uspeva samo ako:

- korisnik postoji
- nije soft deleted
- password je ispravan
- korisnik je `Verified`
- korisnik nije `Blocked`

Refresh token flow:

1. Login vraća access token i refresh token.
2. Refresh token se čuva u bazi.
3. `POST /api/auth/refresh-token` proverava da token postoji, da nije istekao i da nije opozvan.
4. Proverava se da je korisnik i dalje `Verified`, nije `Blocked` i nije soft deleted.
5. Stari refresh token se opoziva.
6. Generišu se novi access token i novi refresh token.
7. `ReplacedByToken` ostaje upisan radi praćenja rotacije.

Važna pravila:

- blocked i unverified korisnici ne mogu login ni refresh
- logout opoziva aktivni refresh token
- ako se opozvani refresh token pokuša ponovo koristiti, sistem opoziva preostale aktivne refresh tokene tog korisnika
- JWT validation dodatno proverava trenutno stanje korisnika u bazi

## Reservation Without Active Membership Rule

Ovo je jedno od ključnih business pravila.

Korisnik sme da rezerviše trening i kada:

- nema aktivan paket
- nema dostupne termine
- ima isteklo članstvo

Razlog:

- korisnik može doći na trening i platiti kod Sare neposredno pre ili posle treninga

Rezervacija proverava samo:

- korisnik je `Verified`
- korisnik nije `Blocked`
- trening nije otkazan
- trening nije u prošlosti
- trening nije popunjen
- korisnik nema duplu rezervaciju za isti trening
- korisnik nema više od 2 naredne rezervacije

Rezervacija ne proverava:

- balance
- aktivnu članarinu
- `CurrentBalance`
- `RemainingSessions`
- broj raspoloživih termina

Balance se proverava tek kada rezervacija postane:

- `Attended`
- `NoShow`

## Membership / Payment Rules

Podržani purchase type-ovi:

- `Package12`
- `Package6`
- `SingleSessions`

Monthly package pravila:

- `Package12` daje 12 termina
- `Package6` daje 6 termina
- oba paketa traže `StartDate`
- ističu na `StartDate.AddMonths(1)`

Single sessions pravila:

- nemaju `EndDate`
- ne ističu
- ako već postoji aktivan single-session balance, novi termini se dodaju na postojeći balance

Session consumption pravilo:

- termin se ne skida pri rezervaciji
- termin se skida tek na `Attended` ili `NoShow`

Prioritet skidanja termina:

1. aktivni mesečni paket (`Package12` ili `Package6`)
2. aktivni `SingleSessions`

Ako korisnik nema termine:

- `Attended` i `NoShow` vraćaju business grešku
- rezervacija i dalje može da postoji pre toga

`Package12` carry-over pravila:

- prenosi se najviše 2 neiskorišćena termina
- gleda se samo neposredno prethodni `Package12`
- carry-over se primenjuje automatski pri kreiranju novog `Package12`

Payment flow:

- kreira se `Payment` zapis
- zatim se kroz `BalanceService` kreira ili dopunjuje odgovarajući balance
- koristi se transakcija za payment + balance workflow

## Hangfire Jobs

Registrovani background job-ovi:

- `AutoMarkAttendanceJob`
- `TrainingReminderJob`
- `MembershipExpirationReminderJob`
- `NotificationEmailJob`

Recurring job registracija:

- `auto-mark-attendance` -> `*/30 * * * *`
- `training-reminders` -> `*/30 * * * *`
- `membership-expiration-reminders` -> `0 9 * * *`

Business značenje:

- `TrainingReminderJob` šalje reminder za rezervisan trening približno 24h pre početka
- `MembershipExpirationReminderJob` šalje reminder 3 dana pre isteka samo za `Package6` i `Package12`
- `AutoMarkAttendanceJob` pokreće auto attendance workflow nad završenim `Reserved` rezervacijama

Dashboard path dolazi iz:

- `HangfireSettings:DashboardPath`

Podrazumevana vrednost:

```text
/hangfire
```

## Swagger Usage

Swagger je uključen u development okruženju.

Koristi se za:

- pregled endpoint-a
- testiranje auth flow-a
- admin i user API pozive
- pregled request/response modela

Tipičan workflow:

1. Otvori `/swagger`
2. Pozovi `POST /api/auth/login`
3. Kopiraj access token
4. Klikni `Authorize`
5. Unesi:

```text
Bearer <access-token>
```

6. Testiraj zaštićene endpoint-e

## Korisni Endpoint-i

```text
GET  /health
POST /api/auth/register
POST /api/auth/login
POST /api/auth/refresh-token
POST /api/auth/logout
GET  /api/auth/me
```

## Napomene

- API koristi global exception middleware
- svi critical datumi i token lifetime logika koriste `DateTime.UtcNow`
- Swagger se ne pali van development-a
- seed identiteta se izvršava pri startup-u, ali ne radi automatske migracije
- connection string i secret vrednosti ne treba hardkodovati u produkciji
