#!/usr/bin/env bash
# Базаның күнделікті бэкапы. Cron арқылы тәулігіне бір рет шақырылады.
# Қолмен: ./scripts/backup.sh
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
DIR="$ROOT/backups"
KEEP_DAYS=14
STAMP="$(date +%Y-%m-%d_%H-%M)"

mkdir -p "$DIR"
cd "$ROOT"

docker compose -f docker-compose.prod.yml exec -T db \
  pg_dump -U salembonus -d salembonus --clean --if-exists \
  | gzip > "$DIR/salembonus_$STAMP.sql.gz"

# Ескі бэкаптарды өшіру
find "$DIR" -name 'salembonus_*.sql.gz' -mtime "+$KEEP_DAYS" -delete

echo "Бэкап дайын: $DIR/salembonus_$STAMP.sql.gz ($(du -h "$DIR/salembonus_$STAMP.sql.gz" | cut -f1))"
