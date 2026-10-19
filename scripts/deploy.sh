#!/usr/bin/env bash
# Серверде жаңа нұсқаны жаю: ./scripts/deploy.sh
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

# 1. Жаңа кодты алып, скрипттің жаңа нұсқасын қайта іске қосамыз:
#    git осы файлды ауыстыруы мүмкін, ескі нұсқаны әрі қарай орындамау үшін.
if [ "${1:-}" != "--pulled" ]; then
  git fetch origin main
  # Серверде deploy.sh қолмен түзетілген, ал дәл сол түзету main-ге түскен болса — жергілікті көшірмені тастаймыз
  if ! git diff --quiet -- scripts/deploy.sh && git diff --quiet FETCH_HEAD -- scripts/deploy.sh; then
    git checkout -- scripts/deploy.sh
  fi
  git merge --ff-only FETCH_HEAD
  exec "$ROOT/scripts/deploy.sh" --pulled
fi

# 2. Серверге тән баптаулар (Traefik, желілер) docker-compose.override.yml-де, ол git-те жоқ.
#    -f анық берілгенде compose оны өзі қоспайды — онсыз сайттар Traefik маршруттарын жоғалтады.
COMPOSE=(docker compose -f docker-compose.prod.yml)
[ -f docker-compose.override.yml ] && COMPOSE+=(-f docker-compose.override.yml)

"${COMPOSE[@]}" up -d --build
docker image prune -f

echo "Күтудеміз..."
sleep 10
"${COMPOSE[@]}" ps
curl -fsS https://api.salemtech.kz/health || true
echo
