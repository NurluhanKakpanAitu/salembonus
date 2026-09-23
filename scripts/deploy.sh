#!/usr/bin/env bash
# Серверде жаңа нұсқаны жаю: ./scripts/deploy.sh
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

git pull --ff-only
docker compose -f docker-compose.prod.yml up -d --build
docker image prune -f

echo "Күтудеміз..."
sleep 10
docker compose -f docker-compose.prod.yml ps
curl -fsS http://localhost/health 2>/dev/null || curl -fsS https://api.salembonus.kz/health || true
echo
