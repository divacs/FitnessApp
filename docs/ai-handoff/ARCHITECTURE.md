# Architecture

## Solution Structure

```text
FitnessApp.sln
FitnessApp.API
FitnessApp.Application
FitnessApp.Domain
FitnessApp.Infrastructure
FitnessApp.Tests
```

## Layer Responsibilities

### FitnessApp.Domain

Contains:

- entities
- enums
- constants
- simple domain defaults

Must not contain:

- EF Core configuration
- HTTP logic
- email logic
- infrastructure dependencies

### FitnessApp.Application

Contains:

- DTOs
- interfaces
- validators
- manual mapper extension methods
- common responses
- common exceptions
- settings models

Examples:

- `ApiResponse<T>`
- `ErrorResponse`
- `PaginatedResponse<T>`
- feature DTO folders
- feature validator folders
- feature mapping folders

### FitnessApp.Infrastructure

Contains:

- `AppDbContext`
- EF Core entity configurations
- service implementations
- Identity seeding
- token service
- email service
- Hangfire jobs
- external/service integrations

Services use `AppDbContext` directly.

### FitnessApp.API

Contains:

- controllers
- middleware
- API service registration
- authentication setup
- authorization setup
- Swagger setup
- request pipeline setup

Controllers must stay thin and call services.

JWT bearer authentication uses standard token validation plus a database-backed user-state check during token validation so blocked, unverified, and soft-deleted users cannot continue using stale access tokens.

### FitnessApp.Tests

Contains backend tests for business logic and service workflows.

## Why There Is No Repository Pattern

The project intentionally uses EF Core directly in service implementations.

Reasons:

- EF Core already provides repository/unit-of-work behavior through `DbSet` and `DbContext`.
- The app is intentionally simple and not enterprise-heavy.
- Adding repositories now would add boilerplate without clear value.
- Service methods can express queries directly and use `AsNoTracking`, projections, includes, transactions and pagination where needed.

## Why There Is No AutoMapper

AutoMapper is intentionally not used.

Reasons:

- mappings should be explicit and easy to read
- DTO shape is small and business-focused
- mapper extension methods avoid hidden mapping behavior
- easier debugging and onboarding

Use manual mapper extension methods only.

Example:

```csharp
public static UserTrainingBalanceResponse ToResponse(this UserTrainingBalance balance)
```

Mapper rules:

- only transform data
- no business logic
- no database access
- no service calls

## DbContext Usage

Services in `FitnessApp.Infrastructure` use `AppDbContext` directly.

Read queries should use:

- `AsNoTracking`
- projections where useful
- pagination for admin lists
- limited includes

Write workflows may use transactions where multiple related changes must be committed together.

## EF Core

Rules:

- use migrations
- use `IEntityTypeConfiguration<T>`
- configure primary keys
- configure relationships
- configure delete behavior
- configure indexes
- configure decimal precision where needed

Important:

- do not automatically run `Database.Migrate()` on startup
- do not cascade delete historical reservations/payments accidentally

## API Response Format

Success:

```json
{
  "data": {},
  "message": "OK"
}
```

Error:

```json
{
  "message": "Poruka greške",
  "errors": []
}
```

Pagination:

```json
{
  "items": [],
  "page": 1,
  "pageSize": 20,
  "totalCount": 100,
  "totalPages": 5
}
```
