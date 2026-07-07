# FitnessApp Backend

FitnessApp is a .NET 8 backend for a small fitness booking and membership system. The focus is on a simple admin workflow, clear business rules, and a clean architecture approach without unnecessary complexity.

## Project Overview

The backend covers:

- user registration and login
- JWT + refresh token authentication
- user verification and blocking
- training sessions and reservations
- memberships, packages, and single sessions
- payment tracking
- email notifications and reminders
- background jobs via Hangfire
- Swagger and a health check endpoint

## Architecture Overview

The solution is split into 5 projects:

- `FitnessApp.API` - Web API entry point, controllers, middleware, auth setup, Swagger, app startup
- `FitnessApp.Application` - DTOs, feature interfaces, validators, mappings, and application contracts
- `FitnessApp.Domain` - entities, enums, constants, and core domain rules
- `FitnessApp.Infrastructure` - EF Core, DbContext, Identity, services, email, Hangfire jobs, and seed logic
- `FitnessApp.Tests` - unit and integration tests

Responsibilities are intentionally separated:

- Domain has no EF/HTTP/email dependencies
- API does not contain business logic
- Infrastructure implements persistence and external services
- Application holds contracts and feature-level structure

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

## How To Run The Project

1. Install:

- .NET 8 SDK
- PostgreSQL

2. Set the connection string in `FitnessApp.API/appsettings.Development.json` or through user secrets.

3. From the root folder, restore and build:

```powershell
dotnet restore
dotnet build
```

4. Apply migrations:

```powershell
dotnet ef database update --project FitnessApp.Infrastructure --startup-project FitnessApp.API --context AppDbContext
```

5. Run the API:

```powershell
dotnet run --project FitnessApp.API
```

For CORS, you can configure multiple frontend origins through `AppSettings:AllowedOrigins`, `AppSettings__AllowedOrigins`, or `APP_ALLOWED_ORIGINS` when using Docker Compose.

Production example:

```text
AppSettings__AllowedOrigins=https://retrofitness.rs
```

Local development example:

```text
AppSettings__AllowedOrigins=https://retrofitness.rs,http://localhost:5173
```

If `AllowedOrigins` is not set, the backend still uses the existing `AppSettings:FrontendUrl` as a fallback for compatibility.

6. In the development environment, use:

- Swagger UI: `https://localhost:<port>/swagger`
- Health check: `https://localhost:<port>/health`
- Hangfire dashboard: `https://localhost:<port>/hangfire`

## Connection String Setup

`appsettings.json` intentionally leaves values empty. A local development example already exists in:

- `FitnessApp.API/appsettings.Development.json`

Example PostgreSQL connection string:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=fitnessappdb;Username=postgres;Password=postgres"
  }
}
```

If you use a different PostgreSQL host, port, database, or credentials, only update `DefaultConnection`.

## User Secrets Setup

It is recommended not to keep sensitive values in `appsettings.Development.json`.

From the root folder:

```powershell
dotnet user-secrets init --project FitnessApp.API
```

Examples of useful secrets:

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

The email service uses Gmail SMTP with:

- host: `smtp.gmail.com`
- port: `587`
- security: `StartTls`

For a Gmail account, a regular password is not enough. You need an App Password:

1. Enable 2-Step Verification on the Gmail account.
2. Open Google Account > Security > App Passwords.
3. Create a new app password for Mail.
4. Set that value in:

- `EmailSettings:SmtpPassword`

Important fields:

- `EmailSettings:SmtpUsername`
- `EmailSettings:SmtpPassword`
- `EmailSettings:FromEmail`
- `EmailSettings:FromName`

For this project, the default sender name is:

```text
Sara - FitnessApp
```

## Migration Commands

Add a new migration:

```powershell
dotnet ef migrations add <MigrationName> --project FitnessApp.Infrastructure --startup-project FitnessApp.API --context AppDbContext
```

Apply migrations:

```powershell
dotnet ef database update --project FitnessApp.Infrastructure --startup-project FitnessApp.API --context AppDbContext
```

List migrations:

```powershell
dotnet ef migrations list --project FitnessApp.Infrastructure --startup-project FitnessApp.API --context AppDbContext
```

The backend does not run automatic migrations in production. Migrations are applied manually.

## Seed Admin Info

At startup, the application:

- seeds the `Admin` and `Korisnik` roles
- seeds the default admin account
- does not attempt seeding if the database is unavailable

The admin seed comes from the `AdminSeed` configuration.

Development defaults:

```json
"AdminSeed": {
  "Email": "admin@example.com",
  "Password": "Development123",
  "FirstName": "Sara",
  "LastName": "Admin"
}
```

The seeded admin is created as:

- `Verified`
- `EmailConfirmed = true`
- a member of the `Admin` role

It is recommended that production uses secure values through environment variables or a secret store.

## Auth / Refresh Token Flow

Main auth endpoints:

```text
POST /api/auth/register
POST /api/auth/login
POST /api/auth/refresh-token
POST /api/auth/logout
GET  /api/auth/me
```

Registration:

- creates a user with `Unverified` status
- assigns the `Korisnik` role
- does not automatically sign the user in

Login succeeds only if:

- the user exists
- the user is not soft deleted
- the password is correct
- the user is `Verified`
- the user is not `Blocked`

Refresh token flow:

1. Login returns an access token and a refresh token.
2. The refresh token is stored in the database.
3. `POST /api/auth/refresh-token` checks that the token exists, has not expired, and has not been revoked.
4. It verifies that the user is still `Verified`, not `Blocked`, and not soft deleted.
5. The old refresh token is revoked.
6. A new access token and a new refresh token are generated.
7. `ReplacedByToken` remains stored for rotation tracking.

Important rules:

- blocked and unverified users cannot log in or refresh
- logout revokes the active refresh token
- if a revoked refresh token is reused, the system revokes the user's remaining active refresh tokens
- JWT validation additionally checks the user's current state in the database

## Reservation Without Active Membership Rule

This is one of the key business rules.

A user is allowed to reserve a training session even when:

- they do not have an active package
- they do not have available sessions
- their membership has expired

Reason:

- the user may still attend the session and pay Sara immediately before or after the training

Reservation checks only:

- the user is `Verified`
- the user is not `Blocked`
- the training is not canceled
- the training is not in the past
- the training is not full
- the user does not already have a duplicate reservation for the same training
- the user does not have more than 2 upcoming reservations

Reservation does not check:

- balance
- active membership
- `CurrentBalance`
- `RemainingSessions`
- number of available sessions

Balance is checked only when the reservation becomes:

- `Attended`
- `NoShow`

## Membership / Payment Rules

Supported purchase types:

- `Package12`
- `Package6`
- `SingleSessions`

Monthly package rules:

- `Package12` gives 12 sessions
- `Package6` gives 6 sessions
- both packages require `StartDate`
- they expire on `StartDate.AddMonths(1)`

Single session rules:

- they do not have an `EndDate`
- they do not expire
- if an active single-session balance already exists, new sessions are added to the existing balance

Session consumption rule:

- a session is not deducted when a reservation is created
- a session is deducted only on `Attended` or `NoShow`

Session deduction priority:

1. active monthly package (`Package12` or `Package6`)
2. active `SingleSessions`

If the user has no sessions:

- `Attended` and `NoShow` return a business error
- the reservation may still exist before that point

`Package12` carry-over rules:

- at most 2 unused sessions are carried over
- only the immediately previous `Package12` is considered
- carry-over is applied automatically when a new `Package12` is created

Payment flow:

- a `Payment` record is created
- then `BalanceService` creates or extends the appropriate balance
- a transaction is used for the payment + balance workflow

## Hangfire Jobs

Registered background jobs:

- `AutoMarkAttendanceJob`
- `TrainingReminderJob`
- `MembershipExpirationReminderJob`
- `NotificationEmailJob`

Recurring job registration:

- `auto-mark-attendance` -> `*/30 * * * *`
- `training-reminders` -> `*/30 * * * *`
- `membership-expiration-reminders` -> `0 9 * * *`

Business meaning:

- `TrainingReminderJob` sends a reminder for a reserved training roughly 24 hours before it starts
- `MembershipExpirationReminderJob` sends a reminder 3 days before expiration only for `Package6` and `Package12`
- `AutoMarkAttendanceJob` runs the auto-attendance workflow over completed `Reserved` reservations

The dashboard path comes from:

- `HangfireSettings:DashboardPath`

Default value:

```text
/hangfire
```

## Swagger Usage

Swagger is enabled in the development environment.

In production, it remains disabled unless it is temporarily enabled explicitly through an environment variable:

```text
SwaggerSettings__Enabled=true
```

After the smoke test, set this value back to `false` or remove the environment variable.

It is used for:

- browsing endpoints
- testing the auth flow
- admin and user API calls
- reviewing request/response models

Typical workflow:

1. Open `/swagger`
2. Call `POST /api/auth/login`
3. Copy the access token
4. Click `Authorize`
5. Enter:

```text
Bearer <access-token>
```

6. Test protected endpoints

## Useful Endpoints

```text
GET  /health
POST /api/auth/register
POST /api/auth/login
POST /api/auth/refresh-token
POST /api/auth/logout
GET  /api/auth/me
```

## Notes

- the API uses global exception middleware
- all critical dates and token lifetime logic use `DateTime.UtcNow`
- Swagger is always enabled in development, and only in production when `SwaggerSettings__Enabled=true`
- identity seeding runs at startup, but automatic migrations do not
- connection strings and secret values should not be hardcoded in production
