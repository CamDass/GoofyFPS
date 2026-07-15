#!/usr/bin/env bash
# ========================================================
# Script de déploiement de GoofyMaster sur le VPS OVH.
# Publie un binaire autonome (self-contained), l'installe sous /opt/goofy-master,
# et (re)démarre le service systemd. À lancer depuis la machine de dev.
#
# Prérequis côté serveur (une seule fois) :
#   sudo adduser --system --group --no-create-home goofymaster
#   sudo mkdir -p /opt/goofy-master && sudo chown goofymaster:goofymaster /opt/goofy-master
#   Copier goofy-master.service dans /etc/systemd/system/ puis: sudo systemctl daemon-reload
#   Copier nginx-goofy-master.conf, activer le site, certbot, sudo systemctl reload nginx
#   Pare-feu : sudo ufw allow 7790/udp   (443/tcp déjà ouvert pour le web)
# ========================================================
set -euo pipefail

SERVER="${1:-user@ovh-server}"          # ex: ./deploy.sh cam@vps.ovh.net
REMOTE_DIR="/opt/goofy-master"
PROJECT_DIR="$(cd "$(dirname "$0")/.." && pwd)"

echo ">> Publication (linux-x64, self-contained)..."
dotnet publish "$PROJECT_DIR/GoofyMaster.csproj" \
    -c Release -r linux-x64 --self-contained true \
    -p:PublishSingleFile=true -p:PublishTrimmed=false \
    -o "$PROJECT_DIR/publish"

echo ">> Envoi vers $SERVER:$REMOTE_DIR ..."
rsync -avz --delete "$PROJECT_DIR/publish/" "$SERVER:$REMOTE_DIR/"
ssh "$SERVER" "sudo chown -R goofymaster:goofymaster $REMOTE_DIR && sudo chmod +x $REMOTE_DIR/GoofyMaster && sudo systemctl restart goofy-master && sleep 2 && sudo systemctl --no-pager status goofy-master | head -n 6"

echo ">> Vérification santé (via le proxy public) :"
echo "   curl https://gfps-api.example.com/v1/health"
