#!/usr/bin/env bash
# ---------------------------------------------------------------------------
# Démarre RabbitMQ (si nécessaire) puis les trois services de la solution.
# Aucun Docker. Les journaux sont écrits dans logs/.
# ---------------------------------------------------------------------------
set -uo pipefail

# shellcheck source=lib.sh
source "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/lib.sh"

echo "=== Atelier 8 — démarrage des services ==="
echo "Racine : $ROOT_DIR"

require_command dotnet "Installer le SDK .NET 7 (voir docs/INSTALLATION_MACOS.md)." || exit 1
require_command sqlite3 "Installer SQLite (fourni avec macOS)." || exit 1
require_command curl "curl est requis pour les vérifications HTTP." || exit 1

# --- RabbitMQ ---------------------------------------------------------------
if rabbitmq_is_up; then
  echo "RabbitMQ : déjà opérationnel sur $RABBITMQ_HOST:$RABBITMQ_PORT."
else
  echo "RabbitMQ : non joignable, tentative de démarrage via Homebrew..."
  if ! command -v brew >/dev/null 2>&1; then
    echo "ERREUR : Homebrew introuvable. Voir docs/INSTALLATION_MACOS.md." >&2
    exit 1
  fi
  brew services start rabbitmq >/dev/null 2>&1 || true

  if ! wait_for_port "$RABBITMQ_PORT" 45; then
    echo "ERREUR : RabbitMQ ne répond pas sur le port $RABBITMQ_PORT." >&2
    echo "Consulter : brew services list" >&2
    exit 1
  fi
  echo "RabbitMQ : démarré."
fi

if rabbitmq_management_is_up; then
  echo "Management UI : http://$RABBITMQ_HOST:$RABBITMQ_MANAGEMENT_PORT (guest/guest en local)."
else
  echo "Management UI : plugin de gestion non disponible (facultatif pour l'application)."
fi

# --- Bases SQLite -----------------------------------------------------------
apply_migrations_if_needed() {
  local project="$1" database="$2"

  if [[ -f "$ROOT_DIR/$project/$database" ]]; then
    return 0
  fi

  echo "Base $database absente : application des migrations $project..."
  ( cd "$ROOT_DIR" && dotnet ef database update --project "$project" --startup-project "$project" )
}

apply_migrations_if_needed "$PRODUCTS_API_PROJECT" "Products.db" || exit 1
apply_migrations_if_needed "$ORDERS_API_PROJECT" "Orders.db" || exit 1

# --- Build ------------------------------------------------------------------
if [[ "${SKIP_BUILD:-0}" != "1" ]]; then
  echo "Compilation de la solution..."
  ( cd "$ROOT_DIR" && dotnet build MicroservicesTP.sln --nologo -v quiet ) || exit 1
fi

# --- Services ---------------------------------------------------------------
start_service "$PRODUCTS_API_PROJECT" "$PRODUCTS_PORT" || exit 1
start_service "$ORDERS_API_PROJECT" "$ORDERS_PORT" || exit 1
start_service "$GATEWAY_PROJECT" "$GATEWAY_PORT" || exit 1

wait_for_http "http://localhost:$PRODUCTS_PORT/swagger/index.html" 90 \
  || { echo "ERREUR : ProductsAPI ne répond pas. Voir logs/$PRODUCTS_API_PROJECT.log" >&2; exit 1; }
echo "ProductsAPI : http://localhost:$PRODUCTS_PORT (Swagger : /swagger)"

wait_for_http "http://localhost:$ORDERS_PORT/swagger/index.html" 90 \
  || { echo "ERREUR : OrdersAPI ne répond pas. Voir logs/$ORDERS_API_PROJECT.log" >&2; exit 1; }
echo "OrdersAPI   : http://localhost:$ORDERS_PORT (Swagger : /swagger)"

wait_for_http "http://localhost:$GATEWAY_PORT/apigateway/Products" 90 \
  || { echo "ERREUR : ApiGateway ne répond pas. Voir logs/$GATEWAY_PROJECT.log" >&2; exit 1; }
echo "ApiGateway  : http://localhost:$GATEWAY_PORT/apigateway"

echo
echo "Services démarrés. Arrêt : scripts/stop-all.sh"
