# FitnessApp Project Context

## Overview

FitnessApp is a small-group fitness booking and membership system for trainings led by Sara.

The product goal is a simple, elegant, fast workflow for:

- user registration and verification
- training browsing and reservations
- membership/session balance tracking
- payment recording
- attendance/no-show handling
- reminders and notifications
- admin management

The current project name is:

```text
FitnessApp
```

## Tech Stack

Backend:

- .NET 8 Web API
- ASP.NET Identity
- JWT authentication
- Refresh tokens
- Entity Framework Core
- SQL Server
- MailKit
- Hangfire
- FluentValidation
- Serilog
- Swagger

Frontend target stack:

- React
- TypeScript
- Vite
- Tailwind CSS
- React Router
- TanStack Query
- Axios
- Zustand or Context API
- React Hook Form
- Zod
- date-fns
- sonner or react-hot-toast
- lucide-react

## Architecture

Solution projects:

```text
FitnessApp.API
FitnessApp.Application
FitnessApp.Domain
FitnessApp.Infrastructure
FitnessApp.Tests
```

Layer responsibilities:

- `FitnessApp.Domain`: entities, enums, constants, basic domain concepts.
- `FitnessApp.Application`: DTOs, interfaces, validators, manual mapper extensions, common response models, exceptions.
- `FitnessApp.Infrastructure`: EF Core DbContext/configurations, Identity, services, email, Hangfire jobs, external implementations.
- `FitnessApp.API`: controllers, middleware, API setup, Swagger, authentication/authorization registration.
- `FitnessApp.Tests`: backend tests.

## Rules That Must Never Change

- A session is not consumed when a reservation is created.
- A session is consumed only when a reservation becomes `ATTENDED` or `NO_SHOW`.
- A user can reserve a training without an active membership or available sessions.
- Reservation logic must not check active package, membership expiration, current balance, or remaining sessions.
- `NO_SHOW` is never automatic.
- If Sara does not enter attendance after a training, the system automatically marks `ATTENDED`.
- `NO_SHOW` is manually entered by Sara.
- Users can have at most 2 upcoming reservations.
- Unverified users cannot use the system.
- Blocked users cannot use the system.
- Soft-deleted users cannot use the system.
- No production secrets should be hardcoded.
- API success responses use `ApiResponse<T>`.
- API error responses use `ErrorResponse`.
- Use manual mapper extension methods, not AutoMapper.
- Keep business logic out of controllers.
