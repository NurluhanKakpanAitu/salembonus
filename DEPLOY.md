# Деплой: Cloudflare Pages + Koyeb + Neon

Барлығы тегін тарифте. Нәтиже:

- `https://salembonus.kz` — тұтынушы PWA (Cloudflare Pages)
- `https://api.salembonus.kz` — .NET API (Koyeb, Docker)
- База — Neon Postgres

Барлық үш сервиске GitHub аккаунтымен кіруге болады. Реті маңызды: алдымен база, сосын API, соңында фронт.

---

## 0. Кодты GitHub-қа шығару

GitHub-та жаңа **private** репозиторий жаса (`salembonus`), сосын:

```bash
cd /Users/nurlykhankakpan/RiderProjects/SalemBonus
git remote add origin git@github.com:<username>/salembonus.git
git push -u origin master
```

---

## 1. Neon (база)

1. https://neon.tech → Sign up with GitHub.
2. **New project**: Name `salembonus`, Region **Europe (Frankfurt)**, Postgres 16.
3. Dashboard → **Connection string** → `.NET` форматын таңда, **Pooled connection** қосулы болсын. Мына түрде болады:

   ```
   Host=ep-xxx-pooler.eu-central-1.aws.neon.tech;Database=neondb;Username=neondb_owner;Password=***;SSL Mode=Require;Channel Binding=Require
   ```

4. Осы жолды сақтап қой, Koyeb-ке керек. Кестелерді API өзі жасайды (миграция автоматты қолданылады, демо деректер толады).

Тегін лимит: 0.5 ГБ, 5 минут кірмесе база ұйықтайды, бірінші сұраныс ~1 сек.

---

## 2. Koyeb (API)

1. https://app.koyeb.com → Sign up with GitHub.
2. **Create Service** → **GitHub** → репозиторийді таңда.
3. Баптаулар:
   - **Builder:** Dockerfile
   - **Work directory:** `backend`
   - **Dockerfile location:** `backend/Dockerfile`
   - **Instance:** Free (Nano)
   - **Region:** Frankfurt
   - **Port:** `8000`, protocol HTTP, path `/`
   - **Health check:** HTTP, path `/health`, port `8000`
4. **Environment variables** (Secret ретінде):

   | Атауы | Мәні |
   |---|---|
   | `ConnectionStrings__Default` | Neon-нан алған жол |
   | `Jwt__Key` | кемінде 32 таңбалы кездейсоқ жол (төменде генерация) |
   | `Cors__Origins__0` | `https://salembonus.kz` |
   | `Cors__Origins__1` | `https://www.salembonus.kz` |
   | `ASPNETCORE_ENVIRONMENT` | `Production` |

   JWT кілтін жасау:

   ```bash
   openssl rand -base64 48
   ```

5. **Deploy**. 3-5 минуттан кейін `https://<app>.koyeb.app/health` → `Healthy`.
6. **Domains** → **Add domain** → `api.salembonus.kz`. Koyeb CNAME мәнін көрсетеді (мысалы `xxx.koyeb.app`), оны 4-қадамда DNS-ке қосасың.

Тегін тариф: сервис ~15 минут кірмесе ұйықтайды, ояну 10-20 сек. Демо алдында `https://api.salembonus.kz/health` ашып қой.

Әр `git push` автоматты деплой жасайды.

---

## 3. Cloudflare Pages (фронт)

1. https://dash.cloudflare.com → Sign up.
2. **Workers & Pages** → **Create** → **Pages** → **Connect to Git** → репозиторий.
3. Build баптаулары:
   - **Framework preset:** Vite
   - **Root directory:** `frontend`
   - **Build command:** `npm run build`
   - **Build output directory:** `dist`
   - **Environment variables:** `NODE_VERSION` = `22`
4. **Save and Deploy**. `https://<project>.pages.dev` ашылады.
5. **Custom domains** → `salembonus.kz` және `www.salembonus.kz`. Домен Cloudflare DNS-те болса автоматты бапталады.

API мекенжайы `frontend/.env.production` файлында (`https://api.salembonus.kz/api`), өзгерту керек болса сол жерде.

---

## 4. Домен және DNS

Ең оңайы: доменнің DNS-ін Cloudflare-ге беру.

1. Cloudflare → **Add a domain** → `salembonus.kz` → Free план.
2. Cloudflare берген екі nameserver-ді (мысалы `ada.ns.cloudflare.com`, `bob.ns.cloudflare.com`) тіркеушіде (ps.kz / hoster.kz) доменнің NS жазбаларына қой. Жаңару 10 минуттан 24 сағатқа дейін.
3. Cloudflare DNS жазбалары:

   | Түрі | Аты | Мәні | Proxy |
   |---|---|---|---|
   | CNAME | `@` | `<project>.pages.dev` | Proxied |
   | CNAME | `www` | `<project>.pages.dev` | Proxied |
   | CNAME | `api` | `<app>.koyeb.app` | **DNS only** (сұр бұлт) |

   `api` жазбасында proxy өшірулі болсын, әйтпесе Koyeb сертификат бере алмайды.

4. **SSL/TLS** → режим **Full (strict)**.

Тексеру:

```bash
curl https://api.salembonus.kz/health
```

Браузерде `https://salembonus.kz` → кіру беті → нөмір → код. Продакшнда код экранда көрінбейді (`ReturnCodeInResponse=false`), SMS провайдері қосылғанша Koyeb логынан аласың: **Koyeb → Service → Logs**, `[SMS -> +7...]` жолы.

---

## 5. Продакшн алдындағы тексеру тізімі

- [ ] `Jwt__Key` кездейсоқ және тек Koyeb Secret-те, git-те жоқ
- [ ] Neon connection string тек Koyeb-те
- [ ] `Auth:ReturnCodeInResponse` продакшнда `false` (appsettings.json-да солай)
- [ ] Дүкендердің `ApiKey` мәндері демо емес, нақты кездейсоқ (базада `Stores` кестесін жаңарту)
- [ ] SMS провайдер қосылған (`ISmsSender` іске асыруы)
- [ ] UptimeRobot (тегін) `https://api.salembonus.kz/health`-ті 5 минут сайын пингтейді, сонда API ұйықтамайды

---

## Жергілікті дамыту

Postgres Docker-де (5433 порт, 5432 басқа жобада бос емес болуы мүмкін):

```bash
docker compose up -d db
dotnet run --project backend/SalemBonus.Api --launch-profile http
cd frontend && npm run dev
```

Базаны нөлден бастау:

```bash
docker compose down -v && docker compose up -d db
```

Жаңа миграция:

```bash
cd backend && dotnet ef migrations add <Name> --project SalemBonus.Infrastructure --startup-project SalemBonus.Api --output-dir Persistence/Migrations
```
