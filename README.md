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
- password is valid
- user is not `Blocked`
- user is `Verified`

Blocked and unverified users cannot log in or refresh tokens.

## Refresh Token Flow

Successful login creates:

- a JWT access token
- a cryptographically secure refresh token stored in the database

Refresh token rotation is enabled:

1. Client sends the current refresh token to `POST /api/auth/refresh-token`.
2. API verifies that the token exists, is not expired, and is not revoked.
3. API verifies that the user is still `Verified` and not `Blocked`.
4. The old refresh token is revoked.
5. A new access token and a new refresh token are issued.
6. The old token stores `ReplacedByToken` for traceability.

Logout calls `POST /api/auth/logout` and revokes the submitted refresh token if it is active.

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
- role
- user status

## Database Foundation

Persistence is configured in `FitnessApp.Infrastructure/Persistence/AppDbContext.cs`.

EF Core configuration uses `IEntityTypeConfiguration` classes in `FitnessApp.Infrastructure/Configurations`.

Current migrations:

```text
InitialCreate
AddRefreshTokens
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
