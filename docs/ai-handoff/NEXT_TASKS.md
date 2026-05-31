# Next Tasks

## TASK 62A: Add expired package maintenance job

Cilj:

Dodati background job koji označava istekle mesečne pakete kao expired/inactive.

Zahtevi:

- kreirati `CheckExpiredPackagesJob` ako ne postoji
- pronaći aktivne `Package6` i `Package12` pakete gde je `EndDate < utcNow`
- postaviti `IsExpired = true`
- postaviti `IsActive = false`
- ne dirati `SingleSessions`
- registrovati Hangfire recurring job jednom dnevno
- dodati logging
- build mora prolaziti

Commit message:

```text
feat: add expired package maintenance job
```

## TASK 63A: Add reservation service tests

Cilj:

Pokriti najvažnija reservation business pravila testovima.

Zahtevi:

- testirati da verified user može rezervisati bez active membership-a
- testirati da rezervacija ne skida termin
- testirati limit od 2 naredne rezervacije
- testirati duplicate reservation za isti trening
- testirati blocked user flow
- testirati unverified user flow
- testirati cancelled/past/full training checks
- build i test moraju prolaziti

Commit message:

```text
test: add reservation business rule coverage
```

## TASK 64A: Add attendance and no-show tests

Cilj:

Pokriti session consumption rules za `ATTENDED`, `NO_SHOW` i auto attendance.

Zahtevi:

- testirati da `ATTENDED` skida termin
- testirati da `NO_SHOW` skida termin
- testirati da bez dostupnih termina admin dobija business error
- testirati da auto attendance označava samo `ATTENDED`
- testirati da auto attendance nikada ne označava `NO_SHOW`
- testirati auto block scenario za 2 consecutive no-show ako ima smisla
- build i test moraju prolaziti

Commit message:

```text
test: add attendance and no-show workflow coverage
```

## TASK 65A: Prepare frontend app foundation

Cilj:

Kreirati React/Vite frontend skeleton usklađen sa backend API-jem i design smernicama.

Zahtevi:

- kreirati frontend projekat ako još ne postoji
- podesiti TypeScript, Vite i Tailwind
- dodati routing foundation
- dodati axios API client
- dodati auth token storage strategy
- dodati refresh token interceptor foundation
- dodati osnovni layout za public/user/admin
- dodati pastel retro fitness theme tokens
- ne implementirati sve stranice odmah
- build mora prolaziti

Commit message:

```text
feat: scaffold frontend application foundation
```

## TASK 66A: Frontend auth screens

Cilj:

Implementirati login/register/me flow na frontend-u.

Zahtevi:

- register screen
- login screen
- auth store/context
- current user fetch
- logout
- protected route foundation
- handle unverified/blocked messages
- form validation with Zod
- toast/error handling
- loading states

Commit message:

```text
feat: add frontend auth flow
```

## TASK 67A: Frontend user dashboard

Cilj:

Prikazati korisniku ključne informacije sa `/api/me/dashboard`.

Zahtevi:

- dashboard page
- current balance summary
- active package and expiration
- single sessions remaining
- next reservations
- latest notifications
- expiring membership warning
- loading/empty/error states
- mobile-first layout

Commit message:

```text
feat: add user dashboard UI
```

## TASK 68A: Frontend training and reservation flow

Cilj:

Omogućiti korisniku pregled treninga i rezervaciju.

Zahtevi:

- trainings list/calendar
- training detail if needed
- reserve action
- cancellation action
- show available spots
- do not block UI reservation based on membership balance
- show clear note that payment can be handled with Sara if needed
- loading/empty/error states

Commit message:

```text
feat: add training reservation UI
```

## TASK 69A: Frontend admin users and verification

Cilj:

Omogućiti Sari pregled korisnika i verifikaciju.

Zahtevi:

- admin users list
- pagination/filter/search
- status filter, especially `UNVERIFIED`
- verify action
- block/unblock actions
- confirm dialogs where required
- status badges

Commit message:

```text
feat: add admin user management UI
```

## TASK 70A: Frontend admin reservations

Cilj:

Omogućiti Sari pregled i upravljanje rezervacijama.

Zahtevi:

- reservations list
- filters by date/status/user/training
- mark attended
- mark no-show
- no-show confirm dialog with warning that session is consumed
- show auto-marked attended info
- loading/empty/error states

Commit message:

```text
feat: add admin reservation management UI
```
