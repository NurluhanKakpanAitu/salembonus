# Деплой: Cloudflare Pages + Render + Neon

Барлығы тегін тарифте. Нәтиже:

- `https://salembonus.kz` — тұтынушы PWA (Cloudflare Pages)
- `https://api.salembonus.kz` — .NET API (Render, Docker)
- База — Neon Postgres

Барлық үш сервиске GitHub аккаунтымен кіруге болады. Реті маңызды: алдымен база, сосын API, соңында фронт.

---

## 0. Кодты GitHub-қа шығару

GitHub-та жаңа **private** репозиторий жаса (`salembonus`), сосын:

```bash
cd /Users/nurlykhankakpan/RiderProjects/SalemBonus
git remote add origin https://github.com/NurluhanKakpanAitu/salembonus.git
git push -u origin main
```

---

## 1. Neon (база)

1. https://neon.tech → Sign up with GitHub.
2. **New project**: Name `salembonus`, Region **Europe (Frankfurt)**, Postgres 16.
3. Dashboard → **Connection string** → `.NET` форматын таңда, **Pooled connection** қосулы болсын. Мына түрде болады:

   ```
   Host=ep-xxx-pooler.eu-central-1.aws.neon.tech;Database=neondb;Username=neondb_owner;Password=***;SSL Mode=Require;Channel Binding=Require
   ```

4. Осы жолды сақтап қой, Render-ге керек. Кестелерді API өзі жасайды (миграция автоматты қолданылады, демо деректер толады).

Тегін лимит: 0.5 ГБ, 5 минут кірмесе база ұйықтайды, бірінші сұраныс ~1 сек.

---

## 2. Render (API)

1. https://render.com → **Get Started** → поштамен немесе GitHub-пен тіркел.
2. Dashboard → **New +** → **Web Service**.
3. **Source Code** → **GitHub** → **Connect GitHub** (бірінші рет сұрайды) → `salembonus` репозиторийін таңдап, **Connect**.
4. Баптаулар:
   - **Name:** `salembonus-api`
   - **Region:** Frankfurt (EU Central)
   - **Branch:** `main`
   - **Root Directory:** `backend`
   - **Language / Runtime:** Docker (Dockerfile-ды өзі табады)
   - **Instance Type:** Free
5. **Environment Variables** → **Add Environment Variable**:

   | Атауы | Мәні |
   |---|---|
   | `ConnectionStrings__Default` | Neon-нан алған .NET жолы |
   | `Jwt__Key` | кемінде 32 таңбалы кездейсоқ жол (төменде генерация) |
   | `Cors__Origins__0` | `https://salembonus.kz` |
   | `Cors__Origins__1` | `https://www.salembonus.kz` |
   | `ASPNETCORE_ENVIRONMENT` | `Production` |

   JWT кілтін жасау:

   ```bash
   openssl rand -base64 48
   ```

   `PORT` айнымалысын Render өзі береді, қосудың қажеті жоқ.

6. **Advanced** → **Health Check Path:** `/health`.
7. **Deploy Web Service**. Бірінші билд 5-8 минут. Аяқталғанда `https://salembonus-api.onrender.com/health` → `Healthy`.
8. **Settings** → **Custom Domains** → **Add** → `api.salembonus.kz`. Render CNAME мәнін көрсетеді (`salembonus-api.onrender.com`), оны 4-қадамда DNS-ке қосасың. Сертификатты Render өзі береді.

Тегін тариф: 15 минут кірмесе ұйықтайды, ояну 30-50 сек. Демо алдында `https://api.salembonus.kz/health` ашып қой немесе UptimeRobot-пен пингте.

Әр `git push` автоматты деплой жасайды. Логтар: сервис беті → **Logs**.

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
   | CNAME | `api` | `salembonus-api.onrender.com` | **DNS only** (сұр бұлт) |

   `api` жазбасында proxy өшірулі болсын, сонда Render сертификатты өзі береді.

4. **SSL/TLS** → режим **Full (strict)**.

Тексеру:

```bash
curl https://api.salembonus.kz/health
```

Браузерде `https://salembonus.kz` → кіру беті → нөмір → код. Продакшнда код экранда көрінбейді (`ReturnCodeInResponse=false`), SMS провайдері қосылғанша Render логынан аласың: **Render → Service → Logs**, `[SMS -> +7...]` жолы.

---

## 5. Продакшн алдындағы тексеру тізімі

- [ ] `Jwt__Key` кездейсоқ және тек Render Environment-те, git-те жоқ
- [ ] Neon connection string тек Render-де
- [ ] `Auth:ReturnCodeInResponse` продакшнда `false` (appsettings.json-да солай)
- [ ] `Auth__StaticOtpCode` орта айнымалысы Render-де жоқ (демо кезінде `1234` қойылған болуы мүмкін)
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
