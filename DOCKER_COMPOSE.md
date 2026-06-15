# Docker Compose Local Production-Like Setup

Ovaj setup podize samo:

- `sqlserver`
- `fitnessapp-api`

Frontend nije ukljucen.

## 1. Obavezne environment promenljive

Pre pokretanja postavi sledece vrednosti u shell-u ili u lokalnom `.env` fajlu pored `docker-compose.yml`:

```text
SQL_SA_PASSWORD=
JWT_ISSUER=
JWT_AUDIENCE=
JWT_SECRET=
EMAIL_SMTP_HOST=
EMAIL_SMTP_USERNAME=
EMAIL_SMTP_PASSWORD=
EMAIL_FROM_EMAIL=
ADMIN_SEED_EMAIL=
ADMIN_SEED_PASSWORD=
APP_CONTACT_PHONE=
APP_FRONTEND_URL=
```

Opcione promenljive sa default vrednostima:

```text
JWT_EXPIRATION_MINUTES=60
JWT_REFRESH_TOKEN_EXPIRATION_DAYS=7
EMAIL_SMTP_PORT=587
EMAIL_FROM_NAME=Sara - FitnessApp
HANGFIRE_DASHBOARD_PATH=/hangfire
ADMIN_SEED_FIRST_NAME=Sara
ADMIN_SEED_LAST_NAME=Admin
APP_CANCELLATION_DEADLINE_HOURS=12
APP_DEFAULT_TRAINING_CAPACITY=12
APP_AUTO_MARK_ATTENDANCE_DELAY_MINUTES=60
```

## 2. Primer lokalnog `.env` fajla

```dotenv
SQL_SA_PASSWORD=Your_strong_Sql_Password123
JWT_ISSUER=FitnessApp.LocalProduction
JWT_AUDIENCE=FitnessApp.Client.LocalProduction
JWT_SECRET=replace-with-a-long-random-secret-at-least-32-characters
EMAIL_SMTP_HOST=smtp.gmail.com
EMAIL_SMTP_USERNAME=your-email@gmail.com
EMAIL_SMTP_PASSWORD=your-app-password
EMAIL_FROM_EMAIL=your-email@gmail.com
ADMIN_SEED_EMAIL=sararetrofitness@gmail.com
ADMIN_SEED_PASSWORD=Retrosara123
APP_CONTACT_PHONE=+381000000000
APP_FRONTEND_URL=http://localhost:5173
```

## 3. Pokretanje

Iz root foldera projekta:

```powershell
docker compose up --build
```

API ce biti dostupan na:

```text
http://localhost:8080
```

SQL Server ce biti dostupan na:

```text
localhost,1433
```

Podaci baze se cuvaju u Docker volume-u:

```text
sqlserver-data
```

## 4. Gasenje

```powershell
docker compose down
```

Ako zelis da ugasis i volume sa podacima baze:

```powershell
docker compose down -v
```
