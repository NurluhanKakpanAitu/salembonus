# Серверге орналастыру (ps.kz VPS)

Бәрі бір серверде, Docker Compose арқылы. HTTPS сертификаттарын Caddy өзі алады және жаңартады.

```
salembonus.kz, www      -> Caddy -> web      (SalemBonus PWA, nginx)
api.salembonus.kz       -> Caddy -> api      (.NET)
pos.salemtech.kz        -> Caddy -> pos-web  (SalemPos, nginx)
pos.salemtech.kz/api/*  -> Caddy -> api      (сол API, cookie бір доменде болуы үшін)
                                    api -> db (PostgreSQL 18, тек ішкі желі)
Тауар суреттері         -> Cloudflare R2 (cdn.salemtech.kz)
```

Қазір: PWA — Cloudflare Worker, API — Render, база — Neon. Көшкеннен кейін Neon **тек разработкаға**
қалады, прод базасы серверде болады.

> Командалардағы `ДҮКЕН_ID`, `+7700...`, `СЕРВЕР_IP` сияқты мәндерді өзіңдікімен ауыстыр.
> Құпия мәндерді (парольдер, токендер) чатқа, скриншотқа, git-ке салма.

---

## 0. Алдын ала дайында

| Не | Қайда |
|---|---|
| VPS: Ubuntu 24.04, 2 vCPU, 4 GB RAM, 40 GB диск | ps.kz |
| `salembonus.kz` DNS-іне қолжетімділік | Cloudflare |
| `salemtech.kz` DNS-іне қолжетімділік | домен тұрған жер (Cloudflare болса, жақсы) |
| Neon connection string (Neon → Dashboard → Connect) | Neon |
| Cloudflare R2-ге қолжетімділік | Cloudflare |

Көшу **түнде**, клиент аз кезде жасалады. Render тоқтағаннан DNS ауысқанға дейін 15–30 минут қызмет
жұмыс істемейді.

---

## 1. Серверді дайындау

ps.kz панелінен root паролімен не SSH кілтімен кір:

```bash
ssh root@СЕРВЕР_IP
```

Жеке пайдаланушы жаса да, root-пен енді кірме:

```bash
adduser deploy
usermod -aG sudo deploy
mkdir -p /home/deploy/.ssh && cp ~/.ssh/authorized_keys /home/deploy/.ssh/ 2>/dev/null
chown -R deploy:deploy /home/deploy/.ssh
exit
```

Өз компьютеріңнен кілтті қос (егер ps.kz-те кілт қоспаған болсаң):

```bash
ssh-copy-id deploy@СЕРВЕР_IP
```

Енді `deploy` болып кір:

```bash
ssh deploy@СЕРВЕР_IP
```

Жүйе, уақыт белдеуі, swap (.NET жинағанда 4 GB жетпей қалуы мүмкін):

```bash
sudo apt update && sudo apt upgrade -y
sudo timedatectl set-timezone Asia/Almaty
sudo fallocate -l 2G /swapfile && sudo chmod 600 /swapfile
sudo mkswap /swapfile && sudo swapon /swapfile
echo '/swapfile none swap sw 0 0' | sudo tee -a /etc/fstab
```

Firewall: тек SSH, HTTP, HTTPS:

```bash
sudo ufw allow OpenSSH
sudo ufw allow 80/tcp
sudo ufw allow 443/tcp
sudo ufw enable
```

SSH-ті парольмен кіруден жап (кілтпен кіре алатыныңды тексергеннен кейін ғана):

```bash
sudo sed -i 's/^#\?PasswordAuthentication.*/PasswordAuthentication no/; s/^#\?PermitRootLogin.*/PermitRootLogin no/' /etc/ssh/sshd_config
sudo systemctl restart ssh
```

---

## 2. Docker

```bash
curl -fsSL https://get.docker.com | sudo sh
sudo usermod -aG docker $USER
newgrp docker
docker --version && docker compose version
```

---

## 3. Кодты тарту

Репозиторий қазір ашық (public), сондықтан кілтсіз тартылады:

```bash
sudo mkdir -p /opt/salembonus && sudo chown $USER:$USER /opt/salembonus
git clone https://github.com/NurluhanKakpanAitu/salembonus.git /opt/salembonus
cd /opt/salembonus
chmod +x scripts/*.sh
```

> Репоны кейін private қылсаң: серверде `ssh-keygen -t ed25519 -f ~/.ssh/id_ed25519 -N ""`, ашық кілтті
> GitHub → Settings → **Deploy keys**-ке қос (write access-сіз), сосын
> `git remote set-url origin git@github.com:NurluhanKakpanAitu/salembonus.git`.

---

## 4. Cloudflare R2 (тауар суреттері және бэкап)

Разработкадағы `salem-api-dev` токенінің рұқсаты барлық бакетке жетеді және кілттері бұрын ашылып
қалған. **Продқа оны қолданба.**

1. **R2 → Create bucket**: `salem-media` (суреттер) және `salem-backups` (бэкап).
2. **R2 → Manage API tokens → Create API token**, екі рет:
   - `salem-api-prod`: Object Read & Write, **тек** `salem-media`
   - `salem-backup-prod`: Object Read & Write, **тек** `salem-backups`

   Әрқайсысының Access Key ID мен Secret Access Key мәнін бірден `.env`-ке жаз (5-қадам), олар қайта
   көрсетілмейді.
3. `salem-media` → **Settings**:
   - **Custom Domains** → `cdn.salemtech.kz` (ол үшін `salemtech.kz` Cloudflare-де болуы керек)
   - **CORS policy**:
     ```json
     [{ "AllowedOrigins": ["https://pos.salemtech.kz"], "AllowedMethods": ["PUT", "GET"],
        "AllowedHeaders": ["content-type"], "MaxAgeSeconds": 3600 }]
     ```
4. `salem-backups` → **Settings** → **Object lifecycle rules** → 30 күннен кейін өшіру.
5. Барлығы жұмыс істегеннен кейін (12-қадам) `salem-api-dev` токенін **өшір**, девке тек `salem-media-dev`
   бакетіне шектелген жаңа токен жаса.

Разработкада жүктелген суреттер `salem-media-dev`-те қалады. Продта тауар суреттерін қайта жүктеу керек
болуы мүмкін (сілтемелері dev бакетке қарайды).

---

## 5. Құпия мәндер (`.env`)

```bash
cd /opt/salembonus
cp .env.prod.example .env
sed -i "s|^DB_PASSWORD=.*|DB_PASSWORD=$(openssl rand -base64 24 | tr -d '/+=')|" .env
sed -i "s|^JWT_KEY=.*|JWT_KEY=$(openssl rand -base64 48 | tr -d '\n')|" .env
nano .env
```

`nano`-да толтыр:
- `R2_ACCOUNT_ID` — Cloudflare → R2 → оң жақтағы Account ID
- `R2_ACCESS_KEY_ID`, `R2_SECRET_ACCESS_KEY` — `salem-api-prod` токені
- `BACKUP_R2_BUCKET=salem-backups`, `BACKUP_R2_ACCESS_KEY_ID`, `BACKUP_R2_SECRET_ACCESS_KEY` — `salem-backup-prod`
- `ACME_EMAIL` — сертификат хабарламалары баратын пошта
- `STATIC_OTP_CODE` — төмендегі ескертуді оқы

Сақтау: `Ctrl+O`, `Enter`, `Ctrl+X`. Файлды тек өзің оқи алатын қыл:

```bash
chmod 600 .env
```

> **`STATIC_OTP_CODE` туралы.** SMS жіберуші әлі қосылмаған, сондықтан SalemBonus клиенттері осы тұрақты
> кодпен кіреді. Бұл — **кез келген нөмірге** сол кодпен кіруге болады деген сөз. Нақты клиенттер көбейсе,
> бірінші кезекте SMS жіберушіні қосу керек.

---

## 6. Образдарды жинау

```bash
docker compose -f docker-compose.prod.yml build
```

Бірінші рет 5–10 минут. Әзірге ештеңе іске қоспа.

---

## 7. Көшу терезесі: Render тоқтату және Neon-нан деректі көшіру

Осы сәттен бастап Neon-ға жазылатын жаңа дерек серверге жетпейді, сондықтан алдымен ескі API тоқтайды.

1. **Render** → `salembonus` сервисі → **Settings** → **Suspend service**.
2. Серверде:

```bash
cd /opt/salembonus
./scripts/import-neon.sh
```

Скрипт Neon connection string-ін сұрайды (экранға шықпайды), дампты `backups/neon_import_*.sql.gz`
файлына сақтайды, `yes` деп растағаннан кейін базаға жүктейді және API-ді қосады. Жаңа миграция болса,
API оны өзі қолданады.

Тексер:

```bash
docker compose -f docker-compose.prod.yml logs --tail=30 api
docker compose -f docker-compose.prod.yml exec db psql -U salembonus -d salembonus \
  -c 'select count(*) as customers from "Customers";' \
  -c 'select count(*) as staff from core.staff_users;'
```

Сандар Neon-дағымен бірдей болуы керек.

---

## 8. DNS-ті серверге бағыттау

**`salembonus.kz` (Cloudflare → DNS → Records).** Бар жазбаларды өшір, мыналарды қос:

| Түрі | Аты | Мәні | Proxy |
|---|---|---|---|
| A | `@` | СЕРВЕР_IP | **DNS only** (сұр бұлт) |
| A | `www` | СЕРВЕР_IP | **DNS only** |
| A | `api` | СЕРВЕР_IP | **DNS only** |

Worker доменін алып таста: **Workers & Pages** → `salembonus` → **Domains** → `salembonus.kz` және
`www.salembonus.kz` → **Remove**. Render-дегі `api.salembonus.kz` custom domain-ін де өшір.

**`salemtech.kz`:**

| Түрі | Аты | Мәні | Proxy |
|---|---|---|---|
| A | `pos` | СЕРВЕР_IP | **DNS only** |

Бұлт **сұр** болуы міндетті: Caddy сертификатты тікелей алады. Таралғанын тексер:

```bash
dig +short salembonus.kz @8.8.8.8
dig +short api.salembonus.kz @8.8.8.8
dig +short pos.salemtech.kz @8.8.8.8
```

Үшеуі де СЕРВЕР_IP қайтаруы керек (5–30 минут).

---

## 9. Іске қосу

```bash
cd /opt/salembonus
docker compose -f docker-compose.prod.yml up -d
docker compose -f docker-compose.prod.yml ps
docker compose -f docker-compose.prod.yml logs -f caddy
```

Caddy логында үш домен үшін `certificate obtained successfully` шығуы керек (`Ctrl+C` — логтан шығу).

```bash
curl https://api.salembonus.kz/health
curl -o /dev/null -w "%{http_code}\n" https://salembonus.kz/
curl -o /dev/null -w "%{http_code}\n" https://pos.salemtech.kz/
curl -o /dev/null -w "%{http_code}\n" -X POST -H 'Content-Type: application/json' -d '{}' https://pos.salemtech.kz/api/staff/v1/auth/login
```

Күтілетіні: `Healthy`, `200`, `200`, `400` (соңғысы — API жауап берді, бос сұраныс қате деп танылды).
`ps`-те `api` күйі `healthy` болуы керек.

---

## 10. SalemPos қызметкерлері

**Міндетті қауіпсіздік қадамы.** Neon-нан разработканың демо қызметкерлері де көшті (`+77000000001`,
`+77000000002`). Олардың құпиясөзі ашық репода (`StaffSeeder.cs`) жазылған. Серверде бірден жап:

```bash
cd /opt/salembonus
# Демо кассир — өшіру
docker compose -f docker-compose.prod.yml exec api dotnet SalemBonus.Api.dll pos deactivate --phone +77000000002
# Демо иесі — MKM AUTO нақты клиент болса, жаңа уақытша құпиясөз; болмаса — deactivate
docker compose -f docker-compose.prod.yml exec api dotnet SalemBonus.Api.dll pos reset-password --phone +77000000001
```

`reset-password` жаңа уақытша құпиясөзді бір рет шығарады, барлық сеансты жабады және PIN-ді өшіреді.

**Жаңа дүкен иесін қосу** (тіркелу беті жоқ):

```bash
docker compose -f docker-compose.prod.yml exec api dotnet SalemBonus.Api.dll pos stores
docker compose -f docker-compose.prod.yml exec api dotnet SalemBonus.Api.dll pos create-owner \
  --store ДҮКЕН_ID --phone +77001234567 --first-name Аты --last-name Тегі --org "Ұйым атауы" --bin 123456789012
```

Команда дүкенге ұйым байлайды (әдепкі өлшем бірліктерімен), иесін және «Касса №1»-ді жасайды,
**уақытша құпиясөзді бір рет** шығарады. Оны иесіне жеке бер. Иесі `https://pos.salemtech.kz`-ке кіреді,
профильде құпиясөзді ауыстырады, PIN қояды. Кассаны құрылғыға иесі өзі тіркейді: сол құрылғыда кіріп,
«Касса» бөлімінде кассаны таңдайды.

Қызметкер құпиясөзін ұмытса (WhatsApp коды қосылғанша): `pos reset-password --phone ...`.
Қызметкер кетсе: `pos deactivate --phone ...`.

---

## 11. Бэкап

Тәулігіне бір рет, түнгі 4-те:

```bash
crontab -e
```

Соңына қос:

```
0 4 * * * cd /opt/salembonus && ./scripts/backup.sh >> /opt/salembonus/backups/backup.log 2>&1
```

Бірден қолмен тексер:

```bash
./scripts/backup.sh
ls -lh backups/
```

Соңғы жолда «R2-ге жіберілді» шығуы керек. Серверде 14 күн, R2-де 30 күн сақталады.

Қалпына келтіру (базаның қазіргі деректері алмастырылады):

```bash
./scripts/restore.sh backups/salembonus_2026-10-01_04-00.sql.gz
```

Айына бір рет қалпына келтіруді бөлек тест серверде не локалда тексеріп тұр — тексерілмеген бэкап бэкап емес.

---

## 12. Соңғы тексеру және тазалау

- [ ] `https://salembonus.kz` ашылады, клиент кіре алады, картасы мен бонусы көрінеді
- [ ] `https://pos.salemtech.kz` ашылады, иесі кіреді, Статистика мен Касса жұмыс істейді
- [ ] Товарға сурет жүктеледі (R2 CORS пен токен дұрыс)
- [ ] Бір тест сатылым → Статистикада көрінеді → қайтарым
- [ ] `./scripts/backup.sh` R2-ге жіберді
- [ ] Демо қызметкерлер жабылды (10-қадам)
- [ ] Бір тәуліктен кейін бәрі тұрақты болса: Render сервисін **Delete**, `salem-api-dev` R2 токенін **өшір**,
      Neon паролін ауыстыр (Neon → Roles → Reset password) және локал `dotnet user-secrets`-ті жаңарт

Мониторинг: [UptimeRobot](https://uptimerobot.com) (тегін) — `https://api.salembonus.kz/health` және
`https://pos.salemtech.kz/` әр 5 минут сайын, құласа поштаға хабар.

---

## 13. Кері қайтару (бірдеңе дұрыс болмаса)

7–9-қадамдарда ақау шықса және тез түзеле алмаса:

1. Render → сервисті **Resume**.
2. Cloudflare-де DNS-ті бұрынғыға қайтар (Worker домендері, `api` → Render).
3. Neon-дағы дерек 7-қадамдағы күйінде тұр — ештеңе жоғалмайды.

Көшкеннен кейін серверде жасалған жаңа деректер (сатылымдар) Neon-ға қайтпайды, сондықтан кері қайтуды
алғашқы сағаттарда ғана шеш.

---

## 14. Жаңа нұсқаны жаю

Кодты өзгертіп `git push` жасағаннан кейін серверде:

```bash
cd /opt/salembonus && ./scripts/deploy.sh
```

Миграциялар API қосылғанда өзі қолданылады. Үлкен өзгерістің алдында `./scripts/backup.sh` жаса.

---

## Пайдалы командалар

```bash
cd /opt/salembonus
docker compose -f docker-compose.prod.yml ps                        # күйі
docker compose -f docker-compose.prod.yml logs -f api               # API логы
docker compose -f docker-compose.prod.yml logs api | grep WhatsApp  # қызметкер кодтары (WhatsApp қосылғанша)
docker compose -f docker-compose.prod.yml logs api | grep SMS       # клиент кодтары
docker compose -f docker-compose.prod.yml restart api               # API-ді қайта қосу
docker compose -f docker-compose.prod.yml exec db psql -U salembonus -d salembonus   # базаға кіру
docker compose -f docker-compose.prod.yml exec api dotnet SalemBonus.Api.dll pos     # SalemPos командалары
df -h && free -h && docker system df                                # диск пен жады
docker image prune -f                                               # ескі образдарды тазалау
```
