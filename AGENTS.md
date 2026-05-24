# FitnessApp — AI Agent Operating Guide

> Ovaj fajl sadrži globalna pravila, arhitekturu, coding standards i UX smernice za AI agenta (Codex/Claude/GPT) koji radi na projektu FitnessApp.
>
> Agent mora da pročita ovaj fajl pre svakog taska i da ga poštuje tokom implementacije.

---

# 1. Project Vision

FitnessApp je moderan fitness booking i membership sistem za male grupne treninge koje vodi Sara.

Aplikacija treba da izgleda:

- premium
- clean
- elegantno
- pastelno
- retro fitness inspired
- jednostavno za korišćenje
- mobile-first
- brzo i pregledno

Cilj aplikacije NIJE enterprise kompleksnost.

Cilj je:
- jednostavan workflow
- laka administracija
- brz UX
- čitljiv kod
- laka buduća nadogradnja

---

# 2. Tech Stack

## Backend

Koristiti:

- .NET 8 Web API
- ASP.NET Identity
- JWT Authentication
- Entity Framework Core
- SQL Server
- MailKit
- Hangfire
- FluentValidation
- Serilog
- Swagger

## Frontend

Koristiti:

- React
- TypeScript
- Vite
- Tailwind CSS
- React Router
- TanStack Query
- Axios
- Zustand ili Context API
- React Hook Form
- Zod
- date-fns
- sonner ili react-hot-toast
- lucide-react

---

# 3. Architecture Rules

## Backend Architecture

Backend mora imati sledeće projekte:

```text
FitnessApp.API
FitnessApp.Application
FitnessApp.Domain
FitnessApp.Infrastructure
FitnessApp.Tests
```

## Responsibility Rules

### Domain

Sadrži:
- entities
- enums
- constants
- osnovna domain pravila

NE SME sadržati:
- EF Core
- HTTP logiku
- email
- infrastructure dependencies

---

### Application

Sadrži:
- services
- interfaces
- DTOs
- validators
- manual mappers
- business logic

---

### Infrastructure

Sadrži:
- DbContext
- EF konfiguracije
- Identity setup
- Email service
- Hangfire jobs
- external services

---

### API

Sadrži:
- controllers
- middleware
- auth setup
- Swagger setup
- dependency injection

---

# 4. Coding Philosophy

Preferirati:

- jednostavan kod
- eksplicitan kod
- čitljivost
- male metode
- jasna imena
- modularnost
- reusable komponente
- minimum magije

Ne komplikovati bez potrebe.

Ako postoje dva rešenja:
- birati jednostavnije
- birati čitljivije

---

# 5. Backend Rules

## General Rules

- koristiti async/await
- koristiti dependency injection
- koristiti DTOs
- ne vraćati entity direktno
- koristiti DateTime.UtcNow
- koristiti FluentValidation
- koristiti global exception handling
- koristiti cancellation token gde ima smisla

---

## Forbidden Backend Patterns

NE koristiti:

- AutoMapper
- business logiku u controllerima
- static helper haos
- God services
- repository pattern ako nije potreban
- hardkodovane secret-e
- hardkodovane connection stringove

---

## Mapping Rules

Koristiti RUČNE mapper extension klase.

Primer:

```csharp
public static UserDto ToDto(this ApplicationUser user)
```

Mapperi:
- rade samo transformaciju podataka
- ne sadrže business logiku

---

# 6. Database Rules

Koristiti:

- EF Core migrations
- IEntityTypeConfiguration

Dodati:
- indekse
- foreign key constraints
- decimal precision

Primer:

```csharp
builder.Property(x => x.Amount)
    .HasPrecision(18, 2);
```

Koristiti:
- soft delete gde ima smisla

Ne raditi:
- automatske migracije u produkciji

---

# 7. Authentication Rules

Koristiti:
- ASP.NET Identity
- JWT auth

Role:
- Admin
- Korisnik

User status:
- UNVERIFIED
- VERIFIED
- BLOCKED

UNVERIFIED:
- ne može koristiti sistem

BLOCKED:
- ne može koristiti sistem

---

# 8. Business Rules That MUST NEVER Change

## Sessions

Termin se NE skida pri rezervaciji.

Termin se skida tek kada:
- ATTENDED
- NO_SHOW

---

## Reservations

Korisnik može imati maksimalno:

```text
2 naredne rezervacije
```

Ne postoji:
- waitlist
- ACTIVE/INACTIVE status
- limit članova

---

## Attendance

Ako Sara ne unese status nakon treninga:
- sistem automatski označava ATTENDED

Sistem NIKADA automatski ne označava NO_SHOW.

NO_SHOW ručno unosi Sara.

---

# 9. Hangfire Rules

Koristiti Hangfire za:
- emailove
- reminders
- expiration jobs
- attendance jobs

Jobovi:
- CheckExpiredPackagesJob
- CarryOverPackage12SessionsJob
- TrainingReminderJob
- MembershipExpirationReminderJob
- AutoMarkAttendanceJob

---

# 10. Email Rules

Koristiti:
- MailKit
- Gmail SMTP

Email:
```text
sara.retrofitness@gmail.com
```

From Name:
```text
Sara - FitnessApp
```

Password:
- NE hardkodovati
- koristiti User Secrets ili environment variables

Svi emailovi treba da budu:
- HTML
- clean
- minimalistički
- responsive

---

# 11. API Design Rules

## Success Response

```json
{
  "data": {},
  "message": "OK"
}
```

---

## Error Response

```json
{
  "message": "Greška",
  "errors": []
}
```

---

## Pagination

```json
{
  "items": [],
  "page": 1,
  "pageSize": 20,
  "totalCount": 100,
  "totalPages": 5
}
```

---

# 12. Error Handling Rules

Koristiti global middleware.

Ne vraćati:
- stack trace
- interne exception detalje

Koristiti:
- 400
- 401
- 403
- 404
- 409
- 500

Business error poruke:
- na srpskom

---

# 13. Frontend Architecture Rules

Koristiti strukturu:

```text
src/
  api/
  auth/
  components/
  components/ui/
  components/layout/
  features/
  hooks/
  layouts/
  pages/
  routes/
  store/
  types/
  utils/
```

---

# 14. Frontend Design Direction

FitnessApp vizuelni identitet:

- retro fitness vibe
- pastel aesthetics
- elegant wellness feel
- feminine
- soft shadows
- rounded corners
- premium look
- airy spacing

Inspiracija:
- luxury wellness apps
- pilates websites
- retro aerobics aesthetic
- modern SaaS dashboard

---

# 15. Color Palette Rules

## Primary

```text
#9B6EF3
```

Koristiti za:
- CTA
- buttons
- active states

---

## Pink Accent

```text
#F67CA8
```

Koristiti za:
- highlights
- hover
- accents

---

## Cyan Accent

```text
#7FE7F2
```

Koristiti za:
- info
- glow
- reserved states

---

## Background

```text
#FFF8F3
```

Global background.

---

## Cards

```text
#FFFFFF
```

Koristiti za:
- cards
- modals
- tables
- forms

---

# 16. UI/UX Rules

Svaka stranica mora imati:
- loading state
- empty state
- error state

Koristiti:
- skeletons
- toast notifications
- confirm dialogs

---

## Confirm Dialog Required For

- delete
- block user
- unblock user
- cancel reservation
- cancel training
- mark NO_SHOW

---

# 17. Homepage Rules

Homepage mora imati:
- hero sekciju
- veliku retro grupnu fitness sliku
- CTA
- pakete
- kako funkcioniše
- kontakt
- footer

NE prikazivati:
- fake statistics
- “500+ članova”
- “1000 treninga”
- “20 trenera”

---

# 18. Admin Panel Rules

Admin panel mora imati:
- dashboard
- korisnici
- treninzi
- rezervacije
- uplate
- notifikacije
- settings
- terms

Admin dashboard NE SME imati:
```text
Aktivni treninzi
```

---

# 19. Admin UX Rules

## Verifikacija korisnika

Sara verifikuje korisnike na:
- /admin/users
- /admin/users/:id

Mora postojati:
- filter UNVERIFIED
- verify button
- status badge

---

## NO_SHOW

Sara označava NO_SHOW na:
- /admin/reservations

Mora postojati:
- confirm modal
- warning da se skida termin

---

# 20. Frontend Component Rules

Praviti reusable komponente.

UI komponente:
- Button
- Input
- Modal
- Badge
- Card
- Table
- Spinner
- EmptyState

Business komponente:
- UserStatusBadge
- ReservationStatusBadge
- TrainingCard
- PaymentForm

---

# 21. Responsive Rules

Mobile-first.

Desktop:
- sidebar

Mobile:
- bottom nav za user
- collapsible sidebar za admin

Sve mora biti responsive.

---

# 22. API Client Rules

Koristiti:
```text
src/api/client.ts
```

Mora imati:
- axios instance
- auth interceptor
- refresh token logic
- error interceptor

---

# 23. Environment Rules

## Backend

Koristiti:
- appsettings.json
- appsettings.Development.json
- appsettings.Production.json

---

## Frontend

Koristiti:
- .env.development
- .env.production

---

# 24. Logging Rules

Koristiti Serilog.

Logovati:
- exceptions
- failed jobs
- important business events

Ne logovati:
- passwords
- JWT secret
- SMTP password

---

# 25. Testing Rules

Minimalno testirati:
- registration
- login
- reservation limits
- no_show logic
- auto attendance
- balance consumption
- reminders

---

# 26. Naming Rules

Koristiti:
- English za code
- Serbian za UI poruke

Primer:

```csharp
ReservationService
TrainingSession
UserTrainingBalance
MembershipExpirationReminderJob
```

---

# 27. Performance Rules

Ne raditi:
- unnecessary queries
- N+1 queries
- massive includes bez potrebe

Koristiti:
- pagination
- projection
- AsNoTracking gde ima smisla

---

# 28. Security Rules

NE hardkodovati:
- JWT secret
- SMTP password
- connection strings

Koristiti:
- environment variables
- user secrets

Ne vraćati sensitive informacije klijentu.

---

# 29. Task Execution Rules

Kada AI agent dobije task:

1. Pročitaj ovaj fajl.
2. Analiziraj postojeću strukturu.
3. Implementiraj SAMO ono što task traži.
4. Ne refaktorisati nepotrebno.
5. Ne uvoditi novu biblioteku bez potrebe.
6. Održavati postojeći stil projekta.
7. Dodati testove ako task menja business logiku.
8. Navesti šta je urađeno.

---

# 30. Before Finishing Any Task

Obavezno proveriti:

- build prolazi
- nema TypeScript grešaka
- nema C# compile grešaka
- nema unused imports
- nema hardkodovanih secret-a
- validacije postoje
- loading/error state postoji
- kod je formatiran
- naming je konzistentan

---

# 31. Final Goal

FitnessApp treba da bude:
- jednostavan
- elegantan
- brz
- intuitivan
- lako održiv

Sara mora brzo da može da:
- verifikuje korisnike
- upravlja rezervacijama
- označi NO_SHOW
- unese uplatu
- pošalje obaveštenje

Korisnik mora brzo da vidi:
- koliko termina ima
- sledeći trening
- status članarine
- notifikacije
- rezervacije
