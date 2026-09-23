# Бір серверге орналастыру (Google Cloud)

База, API және фронт бір виртуалды серверде, Docker Compose арқылы. HTTPS-ті Caddy өзі алады.

```
salembonus.kz      -> Caddy -> web  (nginx, статикалық PWA)
api.salembonus.kz  -> Caddy -> api  (.NET)
                               api  -> db (Postgres, ішкі желі)
```

Серверде деректер бар, сондықтан **бэкап міндетті**. 10-қадамда cron-ға қойылады.

---

## 1. Виртуалды сервер жасау

Google Cloud Console → **Compute Engine** → **VM instances** → **Create instance**.

| Параметр | Мән |
|---|---|
| Name | `salembonus` |
| Region | `europe-west3` (Frankfurt) |
| Machine type | `e2-medium` (2 vCPU, 4 GB) |
| Boot disk | Ubuntu 24.04 LTS, 30 GB, Balanced persistent disk |
| Firewall | **Allow HTTP traffic** және **Allow HTTPS traffic** — екеуін де белгіле |

**Create** басып, 1-2 минут күт.

Сосын IP-ді тұрақтандыр, әйтпесе сервер қайта қосылғанда өзгереді де, домен бұзылады:
**VPC network** → **IP addresses** → `salembonus` жолындағы External IP → **Reserve** (немесе түрін Ephemeral-дан Static-ке ауыстыр).

Осы IP-ді жазып ал, 8-қадамда керек.

---

## 2. Серверге кіру

VM тізімінде `salembonus` жолындағы **SSH** батырмасын бас, браузерде терминал ашылады. Әрі қарайғы командалар сол жерде теріледі.

---

## 3. Жүйені дайындау

```bash
sudo apt update && sudo apt upgrade -y
sudo timedatectl set-timezone Asia/Almaty
```

Swap қос. Онсыз .NET жинау кезінде жады жетпей қалуы мүмкін:

```bash
sudo fallocate -l 2G /swapfile && sudo chmod 600 /swapfile
sudo mkswap /swapfile && sudo swapon /swapfile
echo '/swapfile none swap sw 0 0' | sudo tee -a /etc/fstab
free -h
```

---

## 4. Docker орнату

```bash
curl -fsSL https://get.docker.com | sudo sh
sudo usermod -aG docker $USER
newgrp docker
docker --version && docker compose version
```

---

## 5. Репозиторийге қол жеткізу

Репозиторий жабық болғандықтан, серверге тек оқуға арналған кілт береміз.

```bash
ssh-keygen -t ed25519 -C "salembonus-server" -f ~/.ssh/id_ed25519 -N ""
cat ~/.ssh/id_ed25519.pub
```

Шыққан жолды толық көшір. GitHub → репозиторий → **Settings** → **Deploy keys** → **Add deploy key**:
Title `server`, Key — сол жол, **Allow write access** белгілемейсің. **Add key**.

---

## 6. Кодты тарту

```bash
sudo mkdir -p /opt/salembonus && sudo chown $USER:$USER /opt/salembonus
git clone git@github.com:NurluhanKakpanAitu/salembonus.git /opt/salembonus
cd /opt/salembonus
```

Бірінші қосылғанда `yes` деп растайсың.

---

## 7. Құпия мәндер

```bash
cp .env.prod.example .env
echo "DB_PASSWORD=$(openssl rand -base64 24 | tr -d '/+=')" >> .env
echo "JWT_KEY=$(openssl rand -base64 48)" >> .env
nano .env
```

Файлда әр мән бір рет қана болсын: жоғарыдағы бос `DB_PASSWORD=` және `JWT_KEY=` жолдарын өшір, төмендегі толтырылғандары қалсын. `ACME_EMAIL` мен `STATIC_OTP_CODE` мәндерін тексер. Сақтау: `Ctrl+O`, `Enter`, `Ctrl+X`.

```bash
cat .env
```

---

## 8. Домендерді серверге бағыттау

Cloudflare → `salembonus.kz` → **DNS** → **Records**. Бар жазбаларды өшіріп, үшеуін қос:

| Түрі | Аты | Мәні | Proxy |
|---|---|---|---|
| A | `@` | сервер IP | **DNS only** (сұр бұлт) |
| A | `www` | сервер IP | **DNS only** |
| A | `api` | сервер IP | **DNS only** |

Үшеуінде де бұлт **сұр** болуы керек: Caddy сертификатты өзі алады, оған тікелей қосылу қажет.

Cloudflare-дегі Worker домендерін де алып таста: **Workers & Pages** → `salembonus` → **Domains** → `salembonus.kz` пен `www.salembonus.kz` жолдарындағы `...` → **Remove**. Render-дегі `api.salembonus.kz` домені де енді керек емес.

DNS таралғанын тексер:

```bash
dig +short salembonus.kz @8.8.8.8
dig +short api.salembonus.kz @8.8.8.8
```

Үшеуі де сервер IP-ін қайтаруы керек. 5-30 минут кетуі мүмкін.

---

## 9. Іске қосу

```bash
cd /opt/salembonus
docker compose -f docker-compose.prod.yml up -d --build
```

Бірінші жинау 5-10 минут. Күйін көру:

```bash
docker compose -f docker-compose.prod.yml ps
docker compose -f docker-compose.prod.yml logs -f caddy
```

Caddy логында сертификат алынғаны жазылады. Тексеру:

```bash
curl https://api.salembonus.kz/health
curl -o /dev/null -w "%{http_code}\n" https://salembonus.kz/
```

Бірінші `Healthy`, екіншісі `200` болуы керек. База бос болғандықтан миграциялар автоматты қолданылып, демо деректер толады.

---

## 10. Бэкап

Тәулігіне бір рет, түнгі 4-те:

```bash
crontab -e
```

Соңына қос:

```
0 4 * * * cd /opt/salembonus && ./scripts/backup.sh >> /var/log/salembonus-backup.log 2>&1
```

Қолмен тексеру:

```bash
./scripts/backup.sh
ls -lh backups/
```

Бэкаптар 14 күн сақталады. Қалпына келтіру:

```bash
./scripts/restore.sh backups/salembonus_2026-09-23_04-00.sql.gz
```

Бэкап серверде жатыр, сондықтан айына бір рет біреуін өз компьютеріңе жүктеп ал:

```bash
gcloud compute scp salembonus:/opt/salembonus/backups/ЖАҢАСЫ.sql.gz . --zone=europe-west3-a
```

---

## 11. Жаңа нұсқаны жаю

Кодты өзгертіп `git push` жасаған соң, серверде:

```bash
cd /opt/salembonus && ./scripts/deploy.sh
```

---

## 12. Neon-дағы деректі көшіру (міндетті емес)

Жаңа база бос басталады және демо деректермен толады. Егер Neon-дағы нақты деректі сақтағың келсе, өз компьютеріңде:

```bash
docker run --rm postgres:16-alpine pg_dump \
  "postgresql://neondb_owner:ПАРОЛЬ@ep-winter-hall-b2m2ho2j-pooler.c-6.eu-central-1.aws.neon.tech/neondb?sslmode=require" \
  --clean --if-exists --no-owner --no-privileges | gzip > neon.sql.gz
```

Файлды серверге жіберіп, қалпына келтір:

```bash
gcloud compute scp neon.sql.gz salembonus:/opt/salembonus/ --zone=europe-west3-a
# серверде:
cd /opt/salembonus && ./scripts/restore.sh neon.sql.gz
```

---

## 13. 90-шы күн

Google-дің тегін несиесі 90 күннен кейін бітеді. Сол күнге дейін не ақылы тарифке көшесің, не басқа серверге көшесің.

Көшу оңай: жаңа серверде 3-9 қадамдарды қайталайсың, сосын соңғы бэкапты `restore.sh` арқылы қалпына келтіресің, DNS-те IP-ді ауыстырасың. Шамамен жарты сағат.

Күнтізбеге 80-шы күнге ескерту қойып қой.

---

## Пайдалы командалар

```bash
docker compose -f docker-compose.prod.yml ps                    # күйі
docker compose -f docker-compose.prod.yml logs -f api           # API логы
docker compose -f docker-compose.prod.yml logs api | grep SMS   # кіру кодтары
docker compose -f docker-compose.prod.yml restart api           # қайта қосу
docker compose -f docker-compose.prod.yml down                  # тоқтату
docker compose -f docker-compose.prod.yml exec db psql -U salembonus -d salembonus   # базаға кіру
df -h && free -h                                                # диск пен жады
```
