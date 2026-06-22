# Backend Progress

Last completed task:

```text
TASK 80A migrate database provider from sql server to postgresql
```

Next task:

```text
TBD
```

Important note:

`A` tasks were adjusted because the business rule changed: users can reserve training without an active membership or available sessions. Session availability is checked only when marking `ATTENDED` or `NO_SHOW`.

## Completed Tasks

- TASK 1: solution structure
- TASK 2: core packages
- TASK 3: environment configuration
- TASK 4: Program.cs setup
- TASK 5: domain enums
- TASK 6: ApplicationUser entity
- TASK 7: core domain entities
- TASK 8: AppDbContext and EF configurations
- TASK 9: DbContext, Identity and role seed foundation
- TASK 10: initial EF Core migration and database setup
- TASK 11: standard API response format
- TASK 12: global exception handling foundation
- TASK 13: auth DTOs, validators and JWT token service foundation
- TASK 14: AuthService with access token and refresh token logic
- TASK 15: AuthController and JWT authentication
- TASK 16: email foundation and MailKit service
- TASK 17: admin user verification
- TASK 18: user profile endpoints
- TASK 19: role constants and authorization helpers
- TASK 20: auth and database foundation cleanup
- TASK 21: membership DTOs
- TASK 22: membership validators
- TASK 23: membership mapping extension methods
- TASK 24: BalanceService foundation
- TASK 25: monthly package creation
- TASK 26: single sessions balance management
- TASK 27: current balance calculation
- TASK 28: session consumption logic
- TASK 29: balance management API endpoints
- TASK 30: payment DTOs, validators and mappings
- TASK 30A: reservation business rule update for reservation without active membership
- TASK 31A: PaymentService foundation
- TASK 32A: PaymentService business workflows
- TASK 33A: admin payment endpoints
- TASK 34A: Package12 carry-over logic
- TASK 35A: membership and payment cleanup
- TASK 36: training session DTOs, validators and mappings
- TASK 37: TrainingService foundation
- TASK 38: training management workflows
- TASK 39: training management API endpoints
- TASK 40A: ReservationService foundation with flexible membership rules
- TASK 41A: reservation cancellation workflow
- TASK 42A: admin reservation management endpoints
- TASK 43A: attended reservation workflow and session consumption
- TASK 44A: no-show workflow and abuse protection
- TASK 45A: automatic attendance marking workflow
- TASK 46A: auto attendance Hangfire job
- TASK 47A: training reminder background job
- TASK 48A: membership expiration reminder job
- TASK 49A: notification service and in-app notifications
- TASK 50A: training cancellation notification workflow
- TASK 51A: terms page management
- TASK 52A: application settings management
- TASK 53A: user dashboard endpoint
- TASK 54A: admin dashboard endpoint
- TASK 55A: dashboard and settings integration stabilization
- TASK 56A: pagination helpers and query utilities
- TASK 57A: standardized API responses across controllers
- TASK 58A: audit fields and soft delete cleanup
- TASK 59A: Serilog structured logging configuration
- TASK 60A: validation pipeline cleanup
- TASK 61A: security hardening for auth flow
- TASK 80A: migrate database provider from sql server to postgresql

## Latest Commit Sequence

Recent completed commits include:

```text
refactor: add pagination helpers and query utilities
refactor: standardize api responses across controllers
refactor: normalize audit fields and soft delete behavior
chore: configure serilog structured logging
refactor: standardize validation pipeline
```

## TASK 61A Notes

Implemented auth hardening without changing the existing architecture:

- login now rejects soft-deleted users
- refresh token flow now rejects blocked, unverified, and soft-deleted users with explicit messages
- refresh token reuse now logs a warning and revokes remaining active refresh tokens for that user
- blocking a user now revokes active refresh tokens immediately
- JWT access tokens now include `jti` and full-name claims in addition to existing identity claims
- protected JWT requests now re-check the current user state from the database during token validation
- auth and user tests were expanded for refresh rotation, revoke/logout, deleted-user, and claim coverage

## TASK 80A Notes

- EF Core provider migrated from SQL Server to PostgreSQL
- Hangfire storage migrated from SQL Server storage to PostgreSQL storage
- Docker Compose now uses a PostgreSQL container with persistent volume
- SQL Server migrations were removed and replaced with a fresh PostgreSQL initial migration before production setup
