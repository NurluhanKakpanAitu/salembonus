#!/usr/bin/env bash
# GitHub Actions деплой кілтін серверге рұқсат ету (бір рет, deploy пайдаланушысымен): ./scripts/allow-ci-deploy.sh
# Кілт тек scripts/deploy.sh іске қоса алады: shell, порт және агент форвардинг жабық.
set -euo pipefail

KEY="ssh-ed25519 AAAAC3NzaC1lZDI1NTE5AAAAIH1SJNWXIa1v/JDGzEAdKbLLjhKWG0BzwFVt3GIhXHAX github-actions-salembonus-deploy"
LINE="command=\"cd /opt/salembonus && ./scripts/deploy.sh\",no-port-forwarding,no-agent-forwarding,no-X11-forwarding,no-pty $KEY"

mkdir -p ~/.ssh && chmod 700 ~/.ssh
touch ~/.ssh/authorized_keys && chmod 600 ~/.ssh/authorized_keys

if grep -qF "$KEY" ~/.ssh/authorized_keys; then
  echo "Кілт бұрыннан қосылған"
else
  echo "$LINE" >> ~/.ssh/authorized_keys
  echo "Кілт қосылды: $(whoami)"
fi
