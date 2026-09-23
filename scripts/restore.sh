#!/usr/bin/env bash
# Бэкаптан қалпына келтіру: ./scripts/restore.sh backups/salembonus_2026-09-23_04-00.sql.gz
set -euo pipefail

FILE="${1:?Бэкап файлын көрсет: ./scripts/restore.sh backups/xxx.sql.gz}"
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

echo "НАЗАР: базадағы қазіргі деректер осы бэкаппен алмастырылады."
read -r -p "Жалғастыру? (yes деп жаз) " ok
[ "$ok" = "yes" ] || { echo "Тоқтатылды"; exit 1; }

gunzip -c "$FILE" | docker compose -f docker-compose.prod.yml exec -T db \
  psql -U salembonus -d salembonus -v ON_ERROR_STOP=1

echo "Қалпына келтірілді. API-ды қайта қосамыз."
docker compose -f docker-compose.prod.yml restart api
