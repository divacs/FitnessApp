# Production Configuration Checklist

Ovaj dokument pokriva backend production konfiguraciju za `FitnessApp.API` bez izmene business logike.

## 1. Appsettings struktura

Backend trenutno koristi sledece sekcije:

- `ConnectionStrings:DefaultConnection`
- `JwtSettings`
- `EmailSettings`
- `HangfireSettings`
- `AdminSeed`
- `AppSettings`
- `Serilog`
- `Logging`

`appsettings.json` i `appsettings.Production.json` vec imaju odgovarajucu strukturu sa placeholder vrednostima, sto je dobro za bezbedan deploy.

## 2. Production-sensitive vrednosti

Ove vrednosti ne smeju ostati hardkodovane u produkcionim fajlovima:

- `ConnectionStrings:DefaultConnection`
- `JwtSettings:Issuer`
- `JwtSettings:Audience`
- `JwtSettings:Secret`
- `EmailSettings:SmtpUsername`
- `EmailSettings:SmtpPassword`
- `EmailSettings:FromEmail`
- `AdminSeed:Email`
- `AdminSeed:Password`
- `AdminSeed:FirstName`
- `AdminSeed:LastName`
- `AppSettings:FrontendUrl`
- `AppSettings:ContactPhone`

Napomena:

- `EmailSettings:SmtpHost` je trenutno `smtp.gmail.com` u production fajlu i nije tajna, ali mora biti tacna za provajdera koji se koristi.
- `EmailSettings:FromName` trenutno je `Sara - FitnessApp` i nije sensitive vrednost.
- `HangfireSettings:DashboardPath` nije tajna, ali treba da bude eksplicitno definisan ako se dashboard bude izlagao.

## 3. JWT konfiguracija

JWT konfiguracija je proverena i backend zahteva:

- `Issuer` ne sme biti prazan
- `Audience` ne sme biti prazan
- `Secret` ne sme biti prazan
- `Secret` mora imati najmanje 32 karaktera
- `ExpirationMinutes` mora biti > 0
- `RefreshTokenExpirationDays` mora biti > 0

Pre deploy-a obavezno:

- koristiti dugacak, nasumican secret
- ne koristiti development issuer/audience vrednosti
- cuvati secret u environment variables ili secret store-u

Preporuka:

- koristiti najmanje 32+ karaktera visoke entropije
- rotaciju secreta planirati unapred jer menja validnost postojecih tokena

## 4. EmailSettings konfiguracija

Email servis koristi:

- `MailKit`
- `SecureSocketOptions.StartTls`
- `SmtpHost`
- `SmtpPort`
- opcioni SMTP auth ako postoje `SmtpUsername` i `SmtpPassword`

Bitno za produkciju:

- `SmtpHost` mora biti ispravan
- `SmtpPort` mora biti ispravan
- `FromEmail` mora biti postavljen
- `FromName` mora biti postavljen
- ako SMTP provajder zahteva autentikaciju, `SmtpUsername` i `SmtpPassword` moraju biti setovani

Napomena iz koda:

- startup validacija trenutno proverava `SmtpHost`, `SmtpPort`, `FromEmail` i `FromName`
- `SmtpUsername` i `SmtpPassword` se ne validiraju na startup-u, pa ih treba eksplicitno proveriti u deploy checklisti

Za Gmail SMTP:

- koristiti app password, ne glavni nalog password
- proveriti da je `FromEmail` uskladjen sa nalogom koji salje poruke

## 5. Hangfire konfiguracija

Hangfire je konfigurisan da koristi isti PostgreSQL connection string kao aplikacija.

To znaci:

- `ConnectionStrings:DefaultConnection` mora biti validan i za EF Core i za Hangfire storage
- baza mora dozvoliti kreiranje/koriscenje Hangfire tabela
- aplikacija pri startup-u registruje recurring jobs samo ako baza odgovara na konekciju

Recurring jobs koji se registruju:

- `auto-mark-attendance`
- `training-reminders`
- `membership-expiration-reminders`

Vazna napomena:

- `HangfireSettings:DashboardPath` postoji u konfiguraciji
- Hangfire dashboard trenutno nije mapiran u HTTP pipeline-u
- zato trenutno nema dodatnog produkcionog koraka za dashboard exposure, osim ako se kasnije ne uvede

## 6. Connection string handling

Connection string se cita preko `configuration.GetConnectionString("DefaultConnection")`.

Ako nije postavljen:

- startup baca `InvalidOperationException`
- nece se podici ni EF Core ni Hangfire

Pre deploy-a proveriti:

- da connection string nije ostao prazan u `appsettings.Production.json`
- da se realna vrednost isporucuje preko environment variable ili secret store-a
- da PostgreSQL endpoint, credentials i SSL podesavanja odgovaraju produkcionom okruzenju

Preporuka za environment variable ime:

```text
ConnectionStrings__DefaultConnection
```

## 7. User Secrets / Environment Variables setup

Trenutno stanje:

- repo vec dokumentuje `dotnet user-secrets` za development
- `FitnessApp.API.csproj` trenutno nema committed `UserSecretsId`
- to znaci da se na novoj masini `dotnet user-secrets init --project FitnessApp.API` mora pokrenuti rucno za development

Za produkciju:

- ne koristiti user-secrets kao deployment mehanizam
- koristiti environment variables ili hosting secret store

Preporucena env imena:

```text
ConnectionStrings__DefaultConnection
JwtSettings__Issuer
JwtSettings__Audience
JwtSettings__Secret
JwtSettings__ExpirationMinutes
JwtSettings__RefreshTokenExpirationDays
EmailSettings__SmtpHost
EmailSettings__SmtpPort
EmailSettings__SmtpUsername
EmailSettings__SmtpPassword
EmailSettings__FromEmail
EmailSettings__FromName
HangfireSettings__DashboardPath
AdminSeed__Email
AdminSeed__Password
AdminSeed__FirstName
AdminSeed__LastName
AppSettings__ContactPhone
AppSettings__FrontendUrl
AppSettings__CancellationDeadlineHours
AppSettings__DefaultTrainingCapacity
AppSettings__AutoMarkAttendanceDelayMinutes
ASPNETCORE_ENVIRONMENT
```

Za produkciju postaviti:

```text
ASPNETCORE_ENVIRONMENT=Production
```

## 8. Stavke koje MORAJU biti podesene pre deploy-a

Obavezno:

- postaviti `ASPNETCORE_ENVIRONMENT=Production`
- obezbediti `ConnectionStrings__DefaultConnection`
- obezbediti sve `JwtSettings` vrednosti, posebno `JwtSettings__Secret`
- obezbediti `EmailSettings__FromEmail`
- obezbediti SMTP kredencijale ako provajder zahteva login
- obezbediti sve `AdminSeed` vrednosti
- obezbediti `AppSettings__FrontendUrl` sa validnim absolute URL-om
- obezbediti `AppSettings__ContactPhone`
- proveriti da produkcioni frontend origin odgovara CORS konfiguraciji
- proveriti da baza postoji i da aplikacija moze da se poveze
- proveriti da Hangfire moze da kreira/koristi svoje tabele
- proveriti da admin seed kredencijali nisu development vrednosti
- proveriti da se secrets ne nalaze u git-tracked fajlovima

Preporuceno:

- koristiti secret manager hosting platforme umesto `.json` fajlova
- rotirati JWT i SMTP kredencijale prema operativnoj politici
- ograniciti pristup bazi i SMTP-u samo na potrebne sisteme
- proveriti Serilog sinkove i retention politiku ako se kasnije uvedu dodatni sinkovi

## 9. Kratka procena trenutne spremnosti

Pozitivno:

- konfiguracione sekcije su jasno odvojene
- osetljive production vrednosti su ostavljene prazne u shared config fajlovima
- JWT ima dobru startup validaciju
- connection string fail-fast ponasanje postoji

Paznja pre deploy-a:

- `AdminSeed` je obavezan za startup/seed flow
- `AppSettings:FrontendUrl` je obavezan i direktno utice na CORS
- email auth kredencijali nisu startup-validirani
- `UserSecretsId` nije commitovan u projekat, pa je user-secrets setup trenutno manualan za development
