# FitnessApp

FitnessApp is a clean architecture .NET 8 backend for a modern fitness booking and membership system.

## Architecture

The backend is organized into small projects with clear responsibilities:

- `FitnessApp.API` - ASP.NET Core Web API entry point, controllers, middleware, configuration, and dependency injection.
- `FitnessApp.Application` - application services, interfaces, DTOs, validators, manual mappings, and feature logic.
- `FitnessApp.Domain` - entities, enums, constants, and core domain rules.
- `FitnessApp.Infrastructure` - persistence, external services, identity setup, background jobs, and email infrastructure.
- `FitnessApp.Tests` - unit and integration tests.

## Project Structure

```text
FitnessApp.sln
FitnessApp.API/
  Controllers/
  Middleware/
  Extensions/
  Configurations/
FitnessApp.Application/
  Interfaces/
  Services/
  DTOs/
  Validators/
  Mappings/
  Features/
FitnessApp.Domain/
  Entities/
  Enums/
  Constants/
FitnessApp.Infrastructure/
  Persistence/
  Configurations/
  Identity/
  Services/
  Jobs/
  Emails/
FitnessApp.Tests/
  Unit/
  Integration/
```

## Authentication Flow

FitnessApp uses ASP.NET Identity with JWT access tokens and database-backed refresh tokens.

Main auth endpoints:

```text
POST /api/auth/register
POST /api/auth/login
POST /api/auth/refresh-token
POST /api/auth/logout
GET  /api/auth/me
```

Registration creates a user with:

- `UserStatus = Unverified`
- default role `Korisnik`
- `EmailConfirmed = true`

Registration does not automatically log the user in. Sara must verify the user from the admin users workflow before the user can authenticate successfully.

Login checks:

- email exists
- user is not soft deleted
- password is valid
- user is not `Blocked`
- user is `Verified`

Blocked and unverified users cannot log in or refresh tokens.
Soft-deleted users cannot log in, refresh tokens, or access `/api/auth/me`.

## Refresh Token Flow

Successful login creates:

- a JWT access token
- a cryptographically secure refresh token stored in the database

Refresh token rotation is enabled:

1. Client sends the current refresh token to `POST /api/auth/refresh-token`.
2. API verifies that the token exists, is not expired, and is not revoked.
3. API verifies that the user is still `Verified` and not `Blocked`.
4. API rejects refresh for soft-deleted users.
5. If a revoked refresh token is reused, the API logs a warning and revokes remaining active refresh tokens for that user.
6. The old refresh token is revoked.
7. A new access token and a new refresh token are issued.
8. The old token stores `ReplacedByToken` for traceability.

Logout calls `POST /api/auth/logout` and revokes the submitted refresh token if it is active.
Blocking a user also revokes all currently active refresh tokens for that user.

## Authorization

Roles and authorization constants live in `FitnessApp.Domain/Constants`.

Roles:

- `Admin`
- `Korisnik`

Policies:

- `AdminOnly` - authenticated user with `Admin` role
- `VerifiedUsersOnly` - authenticated user with `userStatus = Verified` claim

JWT tokens include:

- user id
- email
- full name
- role
- user status
- JWT ID (`jti`)

JWT bearer validation also re-checks the current database state of the user on each protected request, so blocked, unverified, and soft-deleted users cannot continue using stale access tokens.

## Database Foundation

Persistence is configured in `FitnessApp.Infrastructure/Persistence/AppDbContext.cs`.

EF Core configuration uses `IEntityTypeConfiguration` classes in `FitnessApp.Infrastructure/Configurations`.

Current migrations:

```text
InitialCreate
AddRefreshTokens
MakeUserTrainingBalanceEndDateNullable
```

Startup seed:

- checks whether the database can be reached
- seeds `Admin` and `Korisnik` roles idempotently
- seeds the default admin user from `AdminSeed` configuration
- does not run automatic migrations

Use EF Core CLI manually for database updates:

```text
dotnet ef database update --project FitnessApp.Infrastructure --startup-project FitnessApp.API --context AppDbContext
```

## Request Pipeline

The API pipeline includes:

- global exception handling
- Serilog request logging
- Swagger in development
- HTTPS redirection
- CORS
- JWT authentication
- authorization policies
- health check at `/health`
- controllers

## Membership Rules

Membership and session balances are handled through `IBalanceService` / `BalanceService`.

Supported purchase types:

- `Package12` - monthly package with 12 sessions
- `Package6` - monthly package with 6 sessions
- `SingleSessions` - individual sessions without expiration

Monthly packages:

- require a `StartDate`
- expire at `StartDate.AddMonths(1)`
- are created as active and not expired
- do not automatically disable older packages, except when `Package12` carry-over is applied

Single sessions:

- require `NumberOfSessions > 0`
- do not have an `EndDate`
- do not expire
- if an active single-session balance already exists, new sessions are added to the existing balance

Current balance calculation:

- counts active non-expired monthly packages with available sessions and `EndDate >= DateTime.UtcNow`
- counts active non-expired single sessions with available sessions
- returns a valid zero-balance response when the user has no available sessions

## Package12 Carry-Over Rules

Only `Package12` can carry over unused sessions.

Carry-over rules:

- maximum 2 unused sessions can be transferred
- transfer happens only when a new `Package12` exists
- only the immediately previous `Package12` is considered
- the new package increases `TotalSessions`, `RemainingSessions`, and `CarriedOverSessions`
- after carry-over, the previous package is marked inactive and expired
- carry-over is idempotent for the new package and will not duplicate transferred sessions

`CreatePackage12Async` applies carry-over automatically after creating the new package.

## Payment Flow

Payments are handled through `IPaymentService` / `PaymentService`.

Creating a payment:

- validates user, amount, payment date, payment type, and required package/session fields
- writes the `Payment` record first
- then creates or updates the matching balance through `IBalanceService`
- uses a database transaction for the payment plus balance workflow

Payment type behavior:

- `Package12` creates a `Package12` balance
- `Package6` creates a `Package6` balance
- `SingleSessions` adds individual sessions

Updating a payment currently allows editing:

- `Amount`
- `PaymentDate`
- `Note`

Deleting a payment currently removes only the payment record. It does not automatically roll back or adjust balances.

## Session Consumption Rules

Sessions are not consumed when a reservation is created.

One session is consumed only when a reservation later becomes:

- `Attended`
- `NoShow`

Consumption priority:

1. active monthly package (`Package12` or `Package6`) with available sessions
2. active single-session balance with available sessions

If no sessions are available, the system throws a business conflict:

```text
Korisnik nema dostupnih termina.
```

Balances remain active even when `RemainingSessions` reaches zero.

## Reservation Without Active Membership

Users may reserve training even when they:

- do not have an active package
- do not have available sessions
- have an expired membership

Reason: the user can attend training and pay Sara immediately before or after the session.

Reservation logic must check only:

- user is `Verified`
- user is not `Blocked`
- training is not cancelled
- training is not in the past
- training is not full
- user does not already have the same training reserved
- user does not have more than 2 upcoming reservations

Reservation logic must not check:

- active package
- membership state
- available session count
- current balance
- remaining sessions
