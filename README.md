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
