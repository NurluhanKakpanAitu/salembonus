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

## SalemPos / касса интеграциясы

Дүкен жағы `X-Store-Api-Key` тақырыбымен жұмыс істейді. Демо кілттер seed-те:

| Дүкен | Кілт | Бонус | Макс. шегеру |
|---|---|---|---|
| MKM AUTO | `sk_test_mkm_auto_11111111` | 5% | 30% |
| Coffee House | `sk_test_coffee_house_2222` | 3% | 50% |
| Beauty Shop | `sk_test_beauty_shop_33333` | 2% | 30% |
| SportLife | `sk_test_sportlife_444444` | 2% | 20% |

Демо тұтынушы: телефон `+77011234567`, QR `SB:SALEM2025X`.

| Метод | Жол | Не істейді |
|---|---|---|
| GET | `/api/pos/store` | Кілт бойынша дүкен, кілтті тексеру |
| GET | `/api/pos/customers/{code}` | Тұтынушыны QR (`SB:XXXX`) немесе телефон бойынша табу, осы дүкендегі балансы мен деңгейі |
| POST | `/api/pos/purchases` | Сатып алуды тіркеу: `redeemAmount` бонусын шегеру, қалған сомаға бонус есептеу |

Ережелер ([BonusRules.cs](backend/SalemBonus.Application/BonusCards/BonusRules.cs)):

- Есептеу = төленген сома × дүкен пайызы, төмен қарай дөңгелектеу.
- Шегеру ≤ баланс және ≤ сатып алу × `MaxRedeemPercent`.
- Деңгей жалпы жұмсалған сома бойынша: 50 000 ₸ Тұрақты, 150 000 ₸ Сүйікті, 500 000 ₸ VIP. Көтерілгенде хабарлама келеді.
- Телефонмен келген белгісіз тұтынушы автоматты тіркеледі, QR коды беріледі, қосымшаны кейін орнатса бонусы дайын тұрады.
- Әр операция транзакция мен хабарлама жазады. Қосымша 15 секунд сайын және экранға оралғанда деректерді жаңартады.

Қателер ProblemDetails түрінде: 400 (валидация), 401 (кілт), 404 (табылмады). Барлық мысалдар [SalemBonus.Api.http](backend/SalemBonus.Api/SalemBonus.Api.http) файлында, Rider-ден тікелей жіберуге болады.
