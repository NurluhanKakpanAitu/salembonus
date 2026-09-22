# SalemBonus

Бір аккаунт — көп мүмкіндіктер. Тұтынушыға арналған ортақ бонус PWA және дүкендерге арналған SalemPos CRM.

## Құрылым

```
backend/
  SalemBonus.Domain          Entities, Enums — тәуелділігі жоқ
  SalemBonus.Application     DTO, сервис интерфейстері мен іске асыруы, репозиторий интерфейстері
  SalemBonus.Infrastructure  EF Core + SQLite, репозиторийлер, миграциялар, seed
  SalemBonus.Api             Controllers, Program.cs, appsettings
frontend/  Vite + React + TypeScript PWA
design/    Pen дизайн файлы (salembonus-app.pen) және PNG экспорттар
```

Тәуелділік бағыты: Api -> Infrastructure -> Application -> Domain. Domain ешкімге тәуелді емес.

## Іске қосу

Backend (http://localhost:5113):

```bash
dotnet run --project backend/SalemBonus.Api
```

Frontend (http://localhost:5173, `/api` сұраныстары backend-ке проксиленеді):

```bash
cd frontend && npm install && npm run dev
```

OpenAPI спецификациясы dev режимде: http://localhost:5113/openapi/v1.json

## Стек

- Backend: ASP.NET Core 9, Clean Architecture, EF Core 9 + SQLite (кейін PostgreSQL), OpenAPI, CORS
- Frontend: React 19, Vite, Tailwind CSS v4, React Router, TanStack Query, Zustand, lucide-react, vite-plugin-pwa
- Домен: salembonus.kz (API: api.salembonus.kz)

## База деректері

Әзірге SQLite, файл `backend/SalemBonus.Api/salembonus.db` (git-ке кірмейді). Қосылғанда миграциялар автоматты қолданылып, демо деректер толтырылады.

Жаңа миграция:

```bash
cd backend && dotnet ef migrations add <Name> --project SalemBonus.Infrastructure --startup-project SalemBonus.Api --output-dir Persistence/Migrations
```

PostgreSQL-ге көшу: `Npgsql.EntityFrameworkCore.PostgreSQL` пакетін қосып, `DependencyInjection.cs`-те `UseSqlite`-ті `UseNpgsql`-ге ауыстыру және миграцияларды қайта генерациялау.
