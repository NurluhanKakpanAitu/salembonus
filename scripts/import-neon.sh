#!/usr/bin/env bash
# Neon-дағы базаны серверге бір рет көшіру: ./scripts/import-neon.sh
# Neon сілтемесі экранға да, тарихқа да жазылмайды — сұралғанда қоясың.
# Алдымен дамп файлға сақталады (backups/neon_import_*.sql.gz), сосын серверге жүктеледі.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"
COMPOSE="docker compose -f docker-compose.prod.yml"
mkdir -p backups
FILE="backups/neon_import_$(date +%Y-%m-%d_%H-%M).sql.gz"

echo "Neon → Connection string (postgresql://...). Экранға шықпайды:"
read -r -s NEON_URL
echo
[ -n "$NEON_URL" ] || { echo "Сілтеме бос"; exit 1; }
# pg_dump пулер (pgbouncer) арқылы емес, тікелей қосылсын
NEON_URL="${NEON_URL/-pooler/}"

echo "1/3 Neon-нан дамп алынуда..."
docker run --rm -e NEON_URL="$NEON_URL" postgres:18-alpine \
  sh -c 'pg_dump "$NEON_URL" --clean --if-exists --no-owner --no-privileges' | gzip > "$FILE"
gzip -t "$FILE"
echo "   Сақталды: $FILE ($(du -h "$FILE" | cut -f1))"

echo "НАЗАР: сервердегі базаның қазіргі деректері Neon-дағымен алмастырылады."
read -r -p "Жалғастыру? (yes деп жаз) " ok
[ "$ok" = "yes" ] || { echo "Тоқтатылды. Дамп файлы қалды: $FILE"; exit 1; }

echo "2/3 API тоқтатылады, база жүктеледі..."
$COMPOSE up -d db
$COMPOSE stop api >/dev/null 2>&1 || true
until $COMPOSE exec -T db pg_isready -U salembonus -d salembonus >/dev/null 2>&1; do sleep 2; done
gunzip -c "$FILE" | $COMPOSE exec -T db psql -q -U salembonus -d salembonus -v ON_ERROR_STOP=1 >/dev/null

echo "3/3 API қосылады (жаңа миграциялар болса, өзі қолданады)..."
$COMPOSE up -d api
echo "Дайын. Тексеру: $COMPOSE logs --tail=50 api"
