# Deployment Guide

Ovaj dokument opisuje kako da se deploy-uje backend deo aplikacije `FitnessApp` u lokalnom i production-like okruzenju.

Obuhvaceno:

- lokalni deploy bez Docker-a
- Docker deploy
- PostgreSQL setup
- environment variables
- EF Core migracije
- seed admin korisnika
- Hangfire ponasanje
- HTTPS smernice
- backup baze

Napomena:

- ovaj guide pokriva backend (`FitnessApp.API`) i PostgreSQL
- frontend nije deo ovog dokumenta

## 1. Preduslovi

Za lokalni deploy bez Docker-a:

- .NET 8 SDK
- PostgreSQL
- `dotnet ef` alat ako nije vec instaliran

Za Docker deploy:

- Docker Desktop ili Docker Engine
- `docker compose`

## 2. Konfiguracione sekcije

Backend koristi sledece konfiguracione sekcije:

- `ConnectionStrings:DefaultConnection`
- `JwtSettings`
- `EmailSettings`
- `HangfireSettings`
- `AdminSeed`
- `AppSettings`
- `Serilog`
- `Logging`

Za produkciju ne treba unositi tajne vrednosti u `appsettings.Production.json`.

## 3. Environment Variables

Preporucene environment promenljive:

```text
ASPNETCORE_ENVIRONMENT=Production
ConnectionStrings__DefaultConnection=
JwtSettings__Issuer=
JwtSettings__Audience=
JwtSettings__Secret=
JwtSettings__ExpirationMinutes=60
JwtSettings__RefreshTokenExpirationDays=7
EmailSettings__SmtpHost=
EmailSettings__SmtpPort=587
EmailSettings__SmtpUsername=
EmailSettings__SmtpPassword=
EmailSettings__FromEmail=
EmailSettings__FromName=Sara - FitnessApp
HangfireSettings__DashboardPath=/hangfire
AdminSeed__Email=
AdminSeed__Password=
AdminSeed__FirstName=Sara
AdminSeed__LastName=Admin
AppSettings__ContactPhone=
AppSettings__AllowedOrigins=
AppSettings__FrontendUrl=
AppSettings__CancellationDeadlineHours=12
AppSettings__DefaultTrainingCapacity=12
AppSettings__AutoMarkAttendanceDelayMinutes=60
```

Obavezno:

- `ConnectionStrings__DefaultConnection`
- svi `JwtSettings` kljucevi
- `EmailSettings__SmtpHost`
- `EmailSettings__FromEmail`
- svi `AdminSeed` kljucevi
- `AppSettings__ContactPhone`
- `AppSettings__AllowedOrigins` ili fallback `AppSettings__FrontendUrl`

Napomena:

- `JwtSettings__Secret` mora imati najmanje 32 karaktera
- SMTP kredencijale cuvati kroz environment variables ili secret store
- `AppSettings__AllowedOrigins` podrzava vise origin-a kao comma-separated lista
- ako `AppSettings__AllowedOrigins` nije setovan, backend koristi `AppSettings__FrontendUrl` kao fallback za CORS
- u `docker-compose.yml` helper env var je `APP_ALLOWED_ORIGINS`, koji se mapira na `AppSettings__AllowedOrigins`

Primer production CORS konfiguracije:

```text
AppSettings__AllowedOrigins=https://retrofitness.rs
```

Primer lokalnog razvoja kada i produkcioni i lokalni frontend treba da pristupe API-ju:

```text
AppSettings__AllowedOrigins=https://retrofitness.rs,http://localhost:5173
```

## 4. PostgreSQL Setup

Backend koristi PostgreSQL i isti connection string koristi i EF Core i Hangfire.

Lokalni primer connection string-a:

```text
Host=localhost;Port=5433;Database=fitnessappdb;Username=postgres;Password=postgres
```

Production primer connection string-a:

```text
Host=YOUR_PG_HOST;Port=5432;Database=fitnessappdb;Username=YOUR_PG_USER;Password=YOUR_PG_PASSWORD;SSL Mode=Require;Trust Server Certificate=false
```

Pre deploy-a proveriti:

- PostgreSQL instanca je dostupna
- baza postoji ili korisnik ima dozvolu da je kreira kroz migracije
- korisnik ima dozvole za tabele, indekse i Hangfire tabele
- network/firewall dozvoljava pristup sa aplikacionog servera
- encryption i sertifikati odgovaraju okruzenju

## 5. Lokalni Deploy Bez Docker-a

Koraci:

1. Postavi environment variables ili development user-secrets.
2. Primeni migracije.
3. Pokreni API.

Migracije:

```powershell
dotnet ef database update --project FitnessApp.Infrastructure --startup-project FitnessApp.API --context AppDbContext
```

Pokretanje:

```powershell
dotnet run --project FitnessApp.API
```

Health check endpoint:

```text
/health
```

Napomena:

- aplikacija koristi `UseHttpsRedirection()`
- u development okruzenju Swagger je ukljucen
- u non-development okruzenjima Swagger se trenutno ne prikazuje

## 6. Docker Deploy

Repo trenutno sadrzi:

- `Dockerfile`
- `docker-compose.yml`
- `DOCKER_COMPOSE.md`

### Docker Image Build

Build:

```powershell
docker build -t fitnessapp-api .
```

Run primer:

```powershell
docker run -p 8080:8080 ^
  -e ASPNETCORE_ENVIRONMENT=Production ^
  -e ConnectionStrings__DefaultConnection="Host=host.docker.internal;Port=5432;Database=fitnessappdb;Username=postgres;Password=YOUR_PASSWORD" ^
  -e JwtSettings__Issuer="FitnessApp.Production" ^
  -e JwtSettings__Audience="FitnessApp.Client.Production" ^
  -e JwtSettings__Secret="replace-with-a-long-random-secret-at-least-32-characters" ^
  -e EmailSettings__SmtpHost="smtp.gmail.com" ^
  -e EmailSettings__SmtpPort="587" ^
  -e EmailSettings__SmtpUsername="your-email@gmail.com" ^
  -e EmailSettings__SmtpPassword="your-app-password" ^
  -e EmailSettings__FromEmail="your-email@gmail.com" ^
  -e AdminSeed__Email="sararetrofitness@gmail.com" ^
  -e AdminSeed__Password="Retrosara123" ^
  -e AdminSeed__FirstName="Sara" ^
  -e AdminSeed__LastName="Admin" ^
  -e AppSettings__ContactPhone="+381000000000" ^
  -e AppSettings__AllowedOrigins="https://retrofitness.rs,http://localhost:5173" ^
  -e AppSettings__FrontendUrl="https://your-frontend-domain.com" ^
  fitnessapp-api
```

### Docker Compose

Za local production-like setup koristi:

```powershell
docker compose up --build
```

Gasenje:

```powershell
docker compose down
```

Compose setup trenutno podize:

- `postgres`
- `fitnessapp-api`

PostgreSQL podaci se cuvaju u named volume-u:

```text
postgres-data
```

## 7. Migracije

Primena svih postojecih migracija:

```powershell
dotnet ef database update --project FitnessApp.Infrastructure --startup-project FitnessApp.API --context AppDbContext
```

Kada pokretati migracije:

- pre prvog deploy-a
- nakon svakog release-a koji sadrzi novu EF migraciju

Preporuka:

- migracije izvrsiti kao eksplicitan deploy korak
- ne oslanjati se na automatske migracije u produkciji

Provera liste migracija:

```powershell
dotnet ef migrations list --project FitnessApp.Infrastructure --startup-project FitnessApp.API --context AppDbContext
```

## 8. Seed Admin Korisnika

Pri startup-u aplikacija:

- seed-uje role
- seed-uje admin korisnika

Admin seed zahteva sledece vrednosti:

- `AdminSeed__Email`
- `AdminSeed__Password`
- `AdminSeed__FirstName`
- `AdminSeed__LastName`

Ako ove vrednosti nisu postavljene:

- startup/seed ce pasti sa konfiguracionom greskom

Ponasenje seed-a:

- ako admin korisnik ne postoji, kreira se
- korisnik se kreira kao `Verified`
- `EmailConfirmed = true`
- korisnik dobija `Admin` rolu

Preporuke:

- koristiti jaku admin lozinku
- ne koristiti development kredencijale u produkciji
- nakon prvog deploy-a bezbedno sacuvati admin pristup

## 9. Hangfire Dashboard Pristup

Trenutno stanje koda:

- Hangfire server je registrovan
- Hangfire koristi PostgreSQL storage
- recurring jobs se registruju pri startup-u ako baza odgovara
- `HangfireSettings__DashboardPath` postoji u konfiguraciji

Vazna napomena:

- Hangfire dashboard trenutno nije mapiran u HTTP pipeline-u
- to znaci da dashboard trenutno nije dostupan preko browser-a, iako je path konfigurisan

Trenutno mozemo potvrditi samo:

- Hangfire storage koristi bazu
- recurring jobs se registruju:
  - `auto-mark-attendance`
  - `training-reminders`
  - `membership-expiration-reminders`

Ako se dashboard bude uvodio kasnije, preporuke su:

- izlagati ga samo iza autentikacije i autorizacije
- ne izlagati ga javno bez zastite
- koristiti HTTPS

## 10. HTTPS Konfiguracija

API koristi:

```text
UseHttpsRedirection()
```

To znaci da produkcioni deploy treba da ima ispravnu HTTPS terminaciju.

Opcije:

- Kestrel sa sertifikatom
- reverse proxy ispred aplikacije, npr. IIS, Nginx, Traefik ili cloud load balancer

Preporuke:

- koristiti validan TLS sertifikat
- proslediti `X-Forwarded-Proto` i ostale proxy headere ako se koristi reverse proxy
- proveriti da API spolja bude dostupan preko `https://`
- proveriti da frontend koristi HTTPS URL za backend

Za Docker:

- cesto je najjednostavnije terminirati HTTPS na reverse proxy sloju
- interni container moze ostati na HTTP portu `8080`, dok je spolja pristup preko HTTPS

## 11. Backup Baze

Backup strategija treba da bude definisana pre produkcionog pustanja.

Minimum preporuke:

- dnevni full backup baze
- cuvanje vise restore tacaka
- odvojena lokacija za backup
- redovno testiranje restore procesa

Sta treba obavezno backup-ovati:

- aplikacionu bazu `fitnessappdb`
- Hangfire tabele koje se nalaze u istoj PostgreSQL bazi

Preporuke za PostgreSQL:

- koristiti `pg_dump`, managed backup ili hosting backup alat
- definisati retention politiku
- cuvati backup van iste masine ako je moguce

Primer logickog plana:

1. Full backup jednom dnevno.
2. Transaction log backup prema RPO zahtevu, ako recovery model to koristi.
3. Periodicni restore test na odvojenoj instanci.

## 12. Post-Deploy Provera

Nakon deploy-a proveriti:

- aplikacija se podigla bez startup gresaka
- `/health` vraca uspeh
- API moze da se poveze na PostgreSQL
- migracije su primenjene
- admin korisnik postoji i moze da se prijavi
- JWT login radi sa produkcionim konfiguracionim vrednostima
- email slanje radi sa realnim SMTP podesavanjima
- recurring jobs su registrovani
- CORS odgovara stvarnom frontend domenu
- secrets nisu zavrsili u git-tracked fajlovima

## 13. Kratak Deploy Redosled

Preporuceni redosled za produkciju:

1. Pripremi PostgreSQL i backup plan.
2. Postavi sve environment variables ili secret store vrednosti.
3. Build/publish ili Docker image.
4. Primeni `dotnet ef database update`.
5. Pokreni aplikaciju.
6. Proveri `/health`.
7. Potvrdi admin login, email i Hangfire job registraciju.
