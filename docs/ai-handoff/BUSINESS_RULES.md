# Business Rules

## Users

Roles:

- `Admin`
- `Korisnik`

User statuses:

- `Unverified`: user exists but cannot use protected user flows.
- `Verified`: user can use the system.
- `Blocked`: user cannot use the system.

Admin verifies users through the admin users workflow.

## Authentication

The backend has JWT authentication and refresh token flow.

Implemented auth concepts:

- registration
- login
- access token generation
- refresh token generation
- refresh token rotation
- refresh token revoke/logout
- refresh token family revocation on suspicious reuse
- refresh token revocation when a user is blocked
- current user endpoint
- role and user status claims

Registration creates an unverified user and does not automatically log the user in.

Login is allowed only for verified and non-blocked users.
Soft-deleted users cannot log in.

Protected JWT requests are revalidated against the current user record.

Rules:

- blocked users cannot continue using old access tokens
- unverified users cannot continue using old access tokens
- soft-deleted users cannot use refresh tokens or protected endpoints

## Memberships and Packages

Supported purchase types:

- `Package12`
- `Package6`
- `SingleSessions`

Package rules:

- `Package12` gives 12 sessions.
- `Package6` gives 6 sessions.
- Monthly packages end one month after `StartDate`.
- `SingleSessions` have no `EndDate`.
- `SingleSessions` do not expire.
- Current balance combines active monthly package sessions and available single sessions.

Carry-over:

- only `Package12` can carry sessions over
- max 2 unused sessions can be carried over
- carry-over applies only when a new `Package12` exists
- carry-over is taken from the immediately previous `Package12`
- previous package is marked inactive/expired after carry-over

## Reservations Without Active Membership

Important adjusted rule:

A user may reserve a training even if:

- they do not have an active package
- they do not have available sessions
- their membership has expired

Reason:

The user may come to training and pay Sara immediately before or after training.

Reservation may check only:

- user exists
- user is `Verified`
- user is not `Blocked`
- training exists
- training is not cancelled
- training is not in the past
- training is not full
- user does not already have a reserved reservation for the same training
- user has no more than 2 upcoming reservations

Reservation must not check:

- active package
- membership state
- current balance
- remaining sessions
- available sessions

## Session Consumption

Sessions are consumed only when a reservation becomes:

- `ATTENDED`
- `NO_SHOW`

Sessions are not consumed when a reservation is created.

Consumption order:

1. active monthly package (`Package12` or `Package6`)
2. active `SingleSessions`

If the user has no available sessions when marking `ATTENDED` or `NO_SHOW`, the admin flow returns:

```text
Korisnik nema dostupnih termina. Prvo evidentirajte uplatu.
```

## Attendance

`ATTENDED` is set when Sara/admin marks the user as attended.

Rules:

- reservation must exist
- status must be `Reserved`
- training must be in progress or finished
- user must have an available session
- session is consumed in this workflow

## NO_SHOW

`NO_SHOW` is manually set by Sara/admin.

Rules:

- reservation must exist
- status must be `Reserved`
- training must be finished
- user must have an available session
- session is consumed in this workflow

`NO_SHOW` is never automatic.

Abuse protection:

- after `NO_SHOW`, the system checks for 2 consecutive no-shows
- if the user also has no active package or available sessions, the user can be automatically blocked
- future notification/email is left as TODO

## Automatic Attendance

If Sara does not enter anything after a training, the system automatically marks `ATTENDED`.

Rules:

- find `Reserved` reservations where training has ended
- wait for `AutoMarkAttendanceDelayMinutes` from app settings
- try to consume one session
- if session exists, mark `ATTENDED`
- set `AutoMarkedAttended = true`
- set `AutoMarkedAt = utcNow`
- if no session exists, log warning
- never mark `NO_SHOW` automatically

## Payment-at-Training Workflow

The system intentionally allows reservation without membership because the user can pay at training.

Payment recording supports:

- `Package12`
- `Package6`
- `SingleSessions`

Creating a payment also creates or updates the corresponding balance through `BalanceService`.

Deleting a payment currently deletes only the payment record and does not roll back balance automatically.

## Notifications

Notification system includes:

- in-app notifications
- global admin notifications
- per-user `UserNotification`
- unread filtering
- notification type filtering
- optional email flag

If `SendEmail == true`, email sending is queued through Hangfire where appropriate.

Training notification support:

- training cancelled
- training updated

When a training is cancelled or updated, reserved users receive in-app notifications and email jobs are queued.

Reservations are not automatically deleted when training is cancelled.

## Email

Email service uses:

- MailKit
- MimeKit
- HTML email
- plain text fallback
- logging

Configured sender:

```text
Sara - FitnessApp
```

SMTP password must not be hardcoded.

## Hangfire Jobs

Currently implemented job files:

- `AutoMarkAttendanceJob`
- `TrainingReminderJob`
- `MembershipExpirationReminderJob`
- `NotificationEmailJob`

Recurring jobs:

- auto attendance every 30 minutes
- training reminders every 30 minutes
- membership expiration reminders daily at 09:00

Training reminders:

- target reserved reservations for trainings starting around 24h ahead
- send only if `ReminderSentAt == null`
- do not send for cancelled, attended, or no-show reservations

Membership expiration reminders:

- only `Package6` and `Package12`
- not for `SingleSessions`
- active packages expiring in 3 days
- send only if `ExpirationReminderSentAt == null`
