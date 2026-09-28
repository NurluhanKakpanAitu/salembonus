#!/usr/bin/env bash
# Базаның күнделікті бэкапы. Cron арқылы тәулігіне бір рет шақырылады.
# Қолмен: ./scripts/backup.sh
# .env-те BACKUP_R2_BUCKET толтырылса, көшірмесі Cloudflare R2-ге де жіберіледі
# (R2-дегі сақтау мерзімі — бакеттің Lifecycle ережесімен, мысалы 30 күн).
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
DIR="$ROOT/backups"
KEEP_DAYS=14
STAMP="$(date +%Y-%m-%d_%H-%M)"
FILE="salembonus_$STAMP.sql.gz"

mkdir -p "$DIR"
cd "$ROOT"

docker compose -f docker-compose.prod.yml exec -T db \
  pg_dump -U salembonus -d salembonus --clean --if-exists \
  | gzip > "$DIR/$FILE"

# Бос не бұзық файл бэкап болып саналмасын
gzip -t "$DIR/$FILE"
[ "$(stat -c %s "$DIR/$FILE" 2>/dev/null || stat -f %z "$DIR/$FILE")" -gt 1024 ] || { echo "Бэкап тым кішкентай: $FILE" >&2; exit 1; }

# Ескі бэкаптарды өшіру
find "$DIR" -name 'salembonus_*.sql.gz' -mtime "+$KEEP_DAYS" -delete

echo "Бэкап дайын: $DIR/$FILE ($(du -h "$DIR/$FILE" | cut -f1))"

# .env-тен бір мәнді оқу (файлды толық source етпейміз)
env_value() { grep -E "^$1=" .env 2>/dev/null | tail -1 | cut -d= -f2- || true; }

BUCKET="$(env_value BACKUP_R2_BUCKET)"
if [ -n "$BUCKET" ]; then
  docker run --rm -v "$DIR:/backups:ro" \
    -e AWS_ACCESS_KEY_ID="$(env_value BACKUP_R2_ACCESS_KEY_ID)" \
    -e AWS_SECRET_ACCESS_KEY="$(env_value BACKUP_R2_SECRET_ACCESS_KEY)" \
    -e AWS_DEFAULT_REGION=auto \
    amazon/aws-cli:2.22.35 s3 cp "/backups/$FILE" "s3://$BUCKET/$FILE" \
    --endpoint-url "https://$(env_value R2_ACCOUNT_ID).r2.cloudflarestorage.com" --only-show-errors
  echo "R2-ге жіберілді: $BUCKET/$FILE"
fi
