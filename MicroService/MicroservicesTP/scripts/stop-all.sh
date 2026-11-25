#!/usr/bin/env bash
# ---------------------------------------------------------------------------
# Arrête les trois services de la solution.
# RabbitMQ reste démarré (service Homebrew) : voir docs/GUIDE_DEMARRAGE.md.
# ---------------------------------------------------------------------------
set -uo pipefail

# shellcheck source=lib.sh
source "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/lib.sh"

echo "=== Atelier 8 — arrêt des services ==="

stop_service "$GATEWAY_PROJECT" "$GATEWAY_PORT"
echo "ApiGateway  : arrêté (port $GATEWAY_PORT)."

stop_service "$ORDERS_API_PROJECT" "$ORDERS_PORT"
echo "OrdersAPI   : arrêté (port $ORDERS_PORT)."

stop_service "$PRODUCTS_API_PROJECT" "$PRODUCTS_PORT"
echo "ProductsAPI : arrêté (port $PRODUCTS_PORT)."

echo
echo "RabbitMQ n'est pas arrêté par ce script."
echo "Pour l'arrêter explicitement : brew services stop rabbitmq"
