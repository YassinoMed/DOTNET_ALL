#!/usr/bin/env bash
# ---------------------------------------------------------------------------
# Test de bout en bout réel : HTTP, Gateway Ocelot, SQLite et RabbitMQ natif.
#
# Le script :
#   1. vérifie les prérequis et démarre les trois services ;
#   2. contrôle la topologie RabbitMQ (exchange fanout + file event-listener) ;
#   3. crée un produit via la Gateway et vérifie sa synchronisation dans les
#      deux bases indépendantes ;
#   4. rejoue le même événement sur le vrai broker et vérifie l'idempotence ;
#   5. exécute le CRUD complet via la Gateway (Products puis Orders) ;
#   6. redémarre les services et vérifie la persistance ;
#   7. vérifie l'indépendance des deux microservices et de leurs bases.
#
# Aucune donnée n'est supprimée en dehors de celle créée par ce script.
# ---------------------------------------------------------------------------
set -uo pipefail

# shellcheck source=lib.sh
source "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/lib.sh"

PRODUCTS_DB="$ROOT_DIR/ProductsAPI/Products.db"
ORDERS_DB="$ROOT_DIR/OrdersAPI/Orders.db"

PRODUCTS_URL="http://localhost:$PRODUCTS_PORT"
ORDERS_URL="http://localhost:$ORDERS_PORT"
GATEWAY_URL="http://localhost:$GATEWAY_PORT"

STAMP="$(date +%Y%m%d-%H%M%S)"
PASS_COUNT=0
FAIL_COUNT=0
FAILED_CHECKS=()
SERVICES_STARTED=0

PASS() { PASS_COUNT=$((PASS_COUNT + 1)); printf '  [PASS] %s\n' "$1"; }
FAIL() { FAIL_COUNT=$((FAIL_COUNT + 1)); FAILED_CHECKS+=("$1"); printf '  [FAIL] %s\n' "$1"; }

CHECK_EQ() { # libellé valeur_observée valeur_attendue
  if [[ "$2" == "$3" ]]; then
    PASS "$1 (observé=$2)"
  else
    FAIL "$1 (attendu=$3, observé=$2)"
  fi
}

CHECK_REGEX() { # libellé valeur expression_régulière
  if [[ "$2" =~ $3 ]]; then
    PASS "$1 (observé=$2)"
  else
    FAIL "$1 (attendu ~/$3/, observé=$2)"
  fi
}

section() { printf '\n=== %s ===\n' "$1"; }

LAST_CODE=""
LAST_BODY=""
request() { # méthode url [corps JSON]
  local method="$1" url="$2" data="${3-}"
  local out
  out="$(mktemp)"

  if [[ -n "$data" ]]; then
    LAST_CODE="$(curl -s -m 30 -o "$out" -w '%{http_code}' -X "$method" "$url" \
      -H 'Content-Type: application/json' --data-binary "$data")"
  else
    LAST_CODE="$(curl -s -m 30 -o "$out" -w '%{http_code}' -X "$method" "$url")"
  fi

  LAST_BODY="$(cat "$out")"
  rm -f "$out"
}

json_field() { # json nom_du_champ
  python3 -c 'import json,sys; print(json.loads(sys.argv[1])[sys.argv[2]])' "$1" "$2" 2>/dev/null
}

rabbit_json() { # chemin API
  curl -s -m 10 -u guest:guest "http://$RABBITMQ_HOST:$RABBITMQ_MANAGEMENT_PORT/api/$1"
}

wait_for_sql() { # base requête valeur_attendue [secondes]
  local database="$1" query="$2" expected="$3" timeout="${4:-30}" i
  for ((i = 0; i < timeout * 4; i++)); do
    if [[ "$(sqlite_scalar "$database" "$query")" == "$expected" ]]; then
      return 0
    fi
    sleep 0.25
  done
  return 1
}

wait_for_rabbit_queue_empty() {
  local i count
  for ((i = 0; i < 40; i++)); do
    count="$(rabbit_json 'queues/%2F/event-listener' \
      | python3 -c 'import json,sys; print(json.load(sys.stdin).get("messages", 0))' 2>/dev/null)"
    [[ "$count" == "0" ]] && return 0
    sleep 0.25
  done
  return 1
}

# MassTransit se connecte de façon asynchrone après la disponibilité HTTP :
# attendre que la file event-listener ait au moins un consumer.
wait_for_rabbit_topology() { # consumers_attendus [secondes]
  local expected="${1:-1}" timeout="${2:-60}" i consumers
  for ((i = 0; i < timeout * 4; i++)); do
    consumers="$(rabbit_json 'queues/%2F/event-listener' \
      | python3 -c 'import json,sys; print(json.load(sys.stdin).get("consumers", 0))' 2>/dev/null)"
    if [[ "${consumers:-0}" -ge "$expected" ]]; then
      return 0
    fi
    sleep 0.25
  done
  return 1
}

wait_for_rabbit_connections() { # connexions_attendues [secondes]
  local expected="${1:-2}" timeout="${2:-60}" i count
  for ((i = 0; i < timeout * 4; i++)); do
    count="$(rabbit_json 'connections' \
      | python3 -c 'import json,sys; print(len(json.load(sys.stdin)))' 2>/dev/null)"
    if [[ "${count:-0}" -ge "$expected" ]]; then
      return 0
    fi
    sleep 0.25
  done
  return 1
}

BUILD_DONE=0
run_start_all() {
  local skip_build="0"
  [[ "$BUILD_DONE" == "1" ]] && skip_build="1"

  if ! SKIP_BUILD="$skip_build" "$ROOT_DIR/scripts/start-all.sh" >>"$LOG_DIR/e2e-start.log" 2>&1; then
    echo "Échec du démarrage des services. Dernières lignes :" >&2
    tail -25 "$LOG_DIR/e2e-start.log" >&2
    return 1
  fi

  BUILD_DONE=1
}

cleanup() {
  if [[ "$SERVICES_STARTED" == "1" ]]; then
    echo
    echo "Nettoyage : arrêt des services démarrés par le script..."
    stop_service "$GATEWAY_PROJECT" "$GATEWAY_PORT"
    stop_service "$ORDERS_API_PROJECT" "$ORDERS_PORT"
    stop_service "$PRODUCTS_API_PROJECT" "$PRODUCTS_PORT"
  fi
}
trap cleanup EXIT

# ---------------------------------------------------------------------------
section "0. Prérequis"
# ---------------------------------------------------------------------------
require_command dotnet "Installer .NET 7." || exit 1
require_command sqlite3 "SQLite est requis." || exit 1
require_command curl "curl est requis." || exit 1
require_command python3 "Python 3 (fourni avec macOS) est requis pour lire le JSON." || exit 1
PASS "Commandes requises présentes (dotnet, sqlite3, curl, python3)."

if rabbitmq_is_up; then PASS "RabbitMQ natif joignable sur $RABBITMQ_HOST:$RABBITMQ_PORT."; else FAIL "RabbitMQ natif non joignable."; fi
if rabbitmq_management_is_up; then PASS "API de gestion RabbitMQ accessible (port $RABBITMQ_MANAGEMENT_PORT)."; else FAIL "API de gestion RabbitMQ inaccessible."; fi

for port in "$PRODUCTS_PORT" "$ORDERS_PORT" "$GATEWAY_PORT"; do
  if port_is_open "$port"; then
    echo "ERREUR : le port $port est déjà occupé. Lancer scripts/stop-all.sh avant le test E2E." >&2
    exit 1
  fi
done
PASS "Ports $PRODUCTS_PORT/$ORDERS_PORT/$GATEWAY_PORT libres avant démarrage."

if [[ -f "$PRODUCTS_DB" && -f "$ORDERS_DB" ]]; then
  PASS "Les deux bases SQLite existent."
else
  FAIL "Une base SQLite est absente (lancer les migrations)."
fi

CHECK_EQ "Table Products de Products.db accessible" \
  "$(sqlite_scalar "$PRODUCTS_DB" "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='Products';")" "1"
CHECK_EQ "Tables Orders et Products de Orders.db accessibles" \
  "$(sqlite_scalar "$ORDERS_DB" "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name IN ('Orders','Products');")" "2"

# ---------------------------------------------------------------------------
section "1. Démarrage des trois services"
# ---------------------------------------------------------------------------
SERVICES_STARTED=1
if ! run_start_all; then
  FAIL "Démarrage de la solution."
  exit 1
fi
PASS "Solution démarrée (ProductsAPI:$PRODUCTS_PORT, OrdersAPI:$ORDERS_PORT, ApiGateway:$GATEWAY_PORT)."

CHECK_EQ "ProductsAPI répond (Swagger)" "$(curl -s -o /dev/null -m 5 -w '%{http_code}' "$PRODUCTS_URL/swagger/index.html")" "200"
CHECK_EQ "OrdersAPI répond (Swagger)" "$(curl -s -o /dev/null -m 5 -w '%{http_code}' "$ORDERS_URL/swagger/index.html")" "200"
CHECK_EQ "ApiGateway répond (route Products)" "$(curl -s -o /dev/null -m 5 -w '%{http_code}' "$GATEWAY_URL/apigateway/Products")" "200"

# ---------------------------------------------------------------------------
section "2. Topologie RabbitMQ (broker réel)"
# ---------------------------------------------------------------------------
EXCHANGE_FACTS="$(rabbit_json 'exchanges' | python3 -c '
import json, sys
data = json.load(sys.stdin)
for exchange in data:
    if exchange["name"] == "Shared.Messages:ProductCreated":
        print(exchange["type"], exchange["durable"])
        break
')"
CHECK_EQ "Exchange fanout ProductCreated présent" "$EXCHANGE_FACTS" "fanout True"

if wait_for_rabbit_topology 1 60; then
  QUEUE_CONSUMERS="$(rabbit_json 'queues/%2F/event-listener' | python3 -c '
import json, sys
print(json.load(sys.stdin).get("consumers", 0))
' 2>/dev/null)"
  CHECK_EQ "File event-listener avec exactement 1 consumer" "$QUEUE_CONSUMERS" "1"
else
  FAIL "Aucun consumer sur event-listener après 60 s (OrdersAPI non connecté à RabbitMQ)."
fi

if wait_for_rabbit_connections 2 60; then
  CONNECTION_COUNT="$(rabbit_json 'connections' | python3 -c 'import json,sys; print(len(json.load(sys.stdin)))')"
  PASS "Connexions MassTransit actives vers RabbitMQ (observé=$CONNECTION_COUNT, attendu>=2)."
else
  FAIL "Connexions MassTransit insuffisantes après 60 s (attendu>=2)."
fi

# ---------------------------------------------------------------------------
section "3. Synchronisation ProductsAPI -> OrdersAPI"
# ---------------------------------------------------------------------------
SYNC_NAME="E2E-Sync-$STAMP"
request POST "$GATEWAY_URL/apigateway/Products" "{\"name\":\"$SYNC_NAME\",\"stockQte\":12,\"price\":123.45}"
CHECK_EQ "POST /apigateway/Products" "$LAST_CODE" "201"
SYNC_ID="$(json_field "$LAST_BODY" productId)"
CHECK_REGEX "Identifiant produit généré" "$SYNC_ID" '^[0-9]+$'

CHECK_EQ "Produit présent dans Products.db" \
  "$(sqlite_scalar "$PRODUCTS_DB" "SELECT COUNT(*) FROM Products WHERE ProductId = $SYNC_ID AND Name = '$SYNC_NAME';")" "1"

if wait_for_sql "$ORDERS_DB" "SELECT COUNT(*) FROM Products WHERE Id = $SYNC_ID AND Name = '$SYNC_NAME';" "1" 30; then
  PASS "ProductCreated consommé : projection présente dans Orders.db."
else
  FAIL "Projection absente de Orders.db après 30 s (consumer ou broker en cause)."
fi

if wait_for_rabbit_queue_empty; then
  PASS "Aucun message en attente dans event-listener."
else
  FAIL "Des messages restent bloqués dans event-listener."
fi

request GET "$GATEWAY_URL/apigateway/Products/$SYNC_ID"
CHECK_EQ "GET /apigateway/Products/$SYNC_ID" "$LAST_CODE" "200"
CHECK_EQ "Nom renvoyé par la Gateway" "$(json_field "$LAST_BODY" name)" "$SYNC_NAME"

# ---------------------------------------------------------------------------
section "4. Idempotence sur le vrai broker (rejeu de ProductCreated)"
# ---------------------------------------------------------------------------
IDEM_NAME="E2E-Idem-$STAMP"
request POST "$GATEWAY_URL/apigateway/Products" "{\"name\":\"$IDEM_NAME\",\"stockQte\":3,\"price\":9.99}"
CHECK_EQ "POST produit d'idempotence" "$LAST_CODE" "201"
IDEM_ID="$(json_field "$LAST_BODY" productId)"

if wait_for_sql "$ORDERS_DB" "SELECT COUNT(*) FROM Products WHERE Id = $IDEM_ID AND Name = '$IDEM_NAME';" "1" 30; then
  PASS "Projection initiale du produit d'idempotence créée."
else
  FAIL "Projection initiale du produit d'idempotence absente."
fi

if RABBITMQ_E2E=1 \
   ORDERS_DB_PATH="$ORDERS_DB" \
   IDEMPOTENCE_PRODUCT_ID="$IDEM_ID" \
   IDEMPOTENCE_PRODUCT_NAME="$IDEM_NAME" \
   dotnet test "$ROOT_DIR/Tests/MicroservicesTP.Tests.csproj" --no-build \
     --filter 'Category=RabbitMq' --logger 'console;verbosity=normal' \
     > "$LOG_DIR/e2e-idempotence.log" 2>&1; then
  PASS "Test xUnit d'idempotence exécuté sur RabbitMQ réel (2 publications)."
else
  FAIL "Test xUnit d'idempotence en échec (voir logs/e2e-idempotence.log)."
  tail -20 "$LOG_DIR/e2e-idempotence.log"
fi

CHECK_EQ "Une seule ligne locale après rejeu" \
  "$(sqlite_scalar "$ORDERS_DB" "SELECT COUNT(*) FROM Products WHERE Id = $IDEM_ID;")" "1"
CHECK_EQ "Nom mis à jour par le rejeu (sans doublon)" \
  "$(sqlite_scalar "$ORDERS_DB" "SELECT Name FROM Products WHERE Id = $IDEM_ID;")" "$IDEM_NAME-updated"

# ---------------------------------------------------------------------------
section "5. CRUD Products via la Gateway"
# ---------------------------------------------------------------------------
CRUD_NAME="E2E-Crud-$STAMP"
CRUD_NAME_UPDATED="$CRUD_NAME-Modifie"
request POST "$GATEWAY_URL/apigateway/Products" "{\"name\":\"$CRUD_NAME\",\"stockQte\":1,\"price\":10.5}"
CHECK_EQ "POST produit CRUD" "$LAST_CODE" "201"
CRUD_ID="$(json_field "$LAST_BODY" productId)"

request GET "$GATEWAY_URL/apigateway/Products/$CRUD_ID"
CHECK_EQ "GET produit CRUD" "$LAST_CODE" "200"
CHECK_EQ "Nom du produit CRUD" "$(json_field "$LAST_BODY" name)" "$CRUD_NAME"

request PUT "$GATEWAY_URL/apigateway/Products/$CRUD_ID" \
  "{\"productId\":$CRUD_ID,\"name\":\"$CRUD_NAME_UPDATED\",\"stockQte\":7,\"price\":77.7}"
CHECK_EQ "PUT produit CRUD" "$LAST_CODE" "204"

request GET "$GATEWAY_URL/apigateway/Products/$CRUD_ID"
CHECK_EQ "GET après PUT" "$(json_field "$LAST_BODY" name)" "$CRUD_NAME_UPDATED"
CHECK_EQ "Stock après PUT dans Products.db" \
  "$(sqlite_scalar "$PRODUCTS_DB" "SELECT StockQte FROM Products WHERE ProductId = $CRUD_ID;")" "7"

request DELETE "$GATEWAY_URL/apigateway/Products/$CRUD_ID"
CHECK_EQ "DELETE produit CRUD" "$LAST_CODE" "204"

request GET "$GATEWAY_URL/apigateway/Products/$CRUD_ID"
CHECK_EQ "GET après DELETE" "$LAST_CODE" "404"
CHECK_EQ "Produit supprimé de Products.db" \
  "$(sqlite_scalar "$PRODUCTS_DB" "SELECT COUNT(*) FROM Products WHERE ProductId = $CRUD_ID;")" "0"

request POST "$GATEWAY_URL/apigateway/Products" '{"name":"","stockQte":1,"price":1}'
CHECK_EQ "Validation : nom vide rejeté" "$LAST_CODE" "400"

request POST "$GATEWAY_URL/apigateway/Products" '{"name":"Prix negatif","stockQte":1,"price":-5}'
CHECK_EQ "Validation : prix négatif rejeté" "$LAST_CODE" "400"

request GET "$GATEWAY_URL/apigateway/Products/999999"
CHECK_EQ "Produit inexistant" "$LAST_CODE" "404"

request GET "$GATEWAY_URL/apigateway/RouteInconnue"
CHECK_EQ "Route Gateway inconnue" "$LAST_CODE" "404"

# ---------------------------------------------------------------------------
section "6. CRUD Orders via la Gateway"
# ---------------------------------------------------------------------------
request POST "$GATEWAY_URL/apigateway/Orders" \
  "{\"orderDate\":\"2026-11-18T10:00:00Z\",\"customerName\":\"E2E Client\",\"productId\":$SYNC_ID}"
CHECK_EQ "POST commande" "$LAST_CODE" "201"
ORDER_ID="$(json_field "$LAST_BODY" ordersId)"

request GET "$GATEWAY_URL/apigateway/Orders/$ORDER_ID"
CHECK_EQ "GET commande" "$LAST_CODE" "200"
CHECK_EQ "Client de la commande" "$(json_field "$LAST_BODY" customerName)" "E2E Client"

request PUT "$GATEWAY_URL/apigateway/Orders/$ORDER_ID" \
  "{\"ordersId\":$ORDER_ID,\"orderDate\":\"2026-11-18T11:00:00Z\",\"customerName\":\"E2E Client Modifie\",\"productId\":$SYNC_ID}"
CHECK_EQ "PUT commande" "$LAST_CODE" "204"

request GET "$GATEWAY_URL/apigateway/Orders/$ORDER_ID"
CHECK_EQ "Client après PUT" "$(json_field "$LAST_BODY" customerName)" "E2E Client Modifie"
CHECK_EQ "Commande mise à jour dans Orders.db" \
  "$(sqlite_scalar "$ORDERS_DB" "SELECT CustomerName FROM Orders WHERE OrdersId = $ORDER_ID;")" "E2E Client Modifie"

request DELETE "$GATEWAY_URL/apigateway/Orders/$ORDER_ID"
CHECK_EQ "DELETE commande" "$LAST_CODE" "204"

request GET "$GATEWAY_URL/apigateway/Orders/$ORDER_ID"
CHECK_EQ "GET commande supprimée" "$LAST_CODE" "404"
CHECK_EQ "Commande supprimée de Orders.db" \
  "$(sqlite_scalar "$ORDERS_DB" "SELECT COUNT(*) FROM Orders WHERE OrdersId = $ORDER_ID;")" "0"

request POST "$GATEWAY_URL/apigateway/Orders" \
  '{"orderDate":"2026-11-18T10:00:00Z","customerName":"Invalide","productId":0}'
CHECK_EQ "Validation : commande sans produit rejetée" "$LAST_CODE" "400"

# ---------------------------------------------------------------------------
section "7. Persistance après redémarrage"
# ---------------------------------------------------------------------------
"$ROOT_DIR/scripts/stop-all.sh" >/dev/null 2>&1
CHECK_EQ "Products.db toujours présent après arrêt" "$([[ -f "$PRODUCTS_DB" ]] && echo oui || echo non)" "oui"
CHECK_EQ "Orders.db toujours présent après arrêt" "$([[ -f "$ORDERS_DB" ]] && echo oui || echo non)" "oui"
CHECK_EQ "Produit de synchronisation toujours en base" \
  "$(sqlite_scalar "$PRODUCTS_DB" "SELECT COUNT(*) FROM Products WHERE ProductId = $SYNC_ID;")" "1"
CHECK_EQ "Projection toujours en base Orders.db" \
  "$(sqlite_scalar "$ORDERS_DB" "SELECT COUNT(*) FROM Products WHERE Id = $SYNC_ID;")" "1"

if ! run_start_all; then
  FAIL "Redémarrage des services."
  exit 1
fi
PASS "Services redémarrés."

request GET "$GATEWAY_URL/apigateway/Products/$SYNC_ID"
CHECK_EQ "Produit relu après redémarrage" "$LAST_CODE" "200"
CHECK_EQ "Nom inchangé après redémarrage" "$(json_field "$LAST_BODY" name)" "$SYNC_NAME"
CHECK_EQ "Projection Orders.db toujours présente après redémarrage" \
  "$(sqlite_scalar "$ORDERS_DB" "SELECT COUNT(*) FROM Products WHERE Id = $IDEM_ID;")" "1"

# ---------------------------------------------------------------------------
section "8. Indépendance des microservices et des bases"
# ---------------------------------------------------------------------------
stop_service "$PRODUCTS_API_PROJECT" "$PRODUCTS_PORT"

request GET "$GATEWAY_URL/apigateway/Orders"
CHECK_EQ "OrdersAPI fonctionne sans ProductsAPI" "$LAST_CODE" "200"

request GET "$GATEWAY_URL/apigateway/Products"
if [[ "$LAST_CODE" != "200" ]]; then
  PASS "Gateway ne renvoie pas de faux succès quand ProductsAPI est arrêté (HTTP $LAST_CODE)."
else
  FAIL "La Gateway renvoie 200 alors que ProductsAPI est arrêté."
fi

start_service "$PRODUCTS_API_PROJECT" "$PRODUCTS_PORT"
if ! wait_for_http "$PRODUCTS_URL/swagger/index.html" 90; then
  FAIL "Redémarrage de ProductsAPI."
  exit 1
fi
request GET "$GATEWAY_URL/apigateway/Products"
CHECK_EQ "ProductsAPI de nouveau opérationnel" "$LAST_CODE" "200"

stop_service "$ORDERS_API_PROJECT" "$ORDERS_PORT"

request GET "$GATEWAY_URL/apigateway/Products"
CHECK_EQ "ProductsAPI fonctionne sans OrdersAPI" "$LAST_CODE" "200"

start_service "$ORDERS_API_PROJECT" "$ORDERS_PORT"
if ! wait_for_http "$ORDERS_URL/swagger/index.html" 90; then
  FAIL "Redémarrage d'OrdersAPI."
  exit 1
fi
request GET "$GATEWAY_URL/apigateway/Orders"
CHECK_EQ "OrdersAPI de nouveau opérationnel" "$LAST_CODE" "200"

# Les commentaires .cs mentionnent volontairement la base de l'autre service
# pour expliquer l'isolation : la vérification porte donc sur les chaînes de
# connexion réellement quotées ("Data Source=..."), pas sur les commentaires.
data_sources_of() { # projet
  grep -R -oE '"Data Source=[^"]+' "$ROOT_DIR/$1" \
    --include='*.cs' --include='*.json' \
    --exclude-dir=bin --exclude-dir=obj 2>/dev/null \
    | sed 's/.*"Data Source=//' | sort -u | paste -sd ' ' -
}

configured_connection_string() { # projet
  python3 -c 'import json,sys; print(json.load(open(sys.argv[1]))["ConnectionStrings"]["DefaultConnection"])' \
    "$ROOT_DIR/$1/appsettings.json" 2>/dev/null
}

CHECK_EQ "ProductsAPI configuré uniquement sur Products.db" \
  "$(configured_connection_string "$PRODUCTS_API_PROJECT")" "Data Source=Products.db"
CHECK_EQ "ProductsAPI ne référence que Products.db (Data Source)" \
  "$(data_sources_of "$PRODUCTS_API_PROJECT")" "Products.db"

CHECK_EQ "OrdersAPI configuré uniquement sur Orders.db" \
  "$(configured_connection_string "$ORDERS_API_PROJECT")" "Data Source=Orders.db"
CHECK_EQ "OrdersAPI ne référence que Orders.db (Data Source)" \
  "$(data_sources_of "$ORDERS_API_PROJECT")" "Orders.db"

# ---------------------------------------------------------------------------
section "Résultat"
# ---------------------------------------------------------------------------
echo "Checks PASS : $PASS_COUNT"
echo "Checks FAIL : $FAIL_COUNT"

if [[ "$FAIL_COUNT" -gt 0 ]]; then
  echo
  echo "Checks en échec :"
  for check in "${FAILED_CHECKS[@]}"; do
    echo "  - $check"
  done
  echo
  echo "Données de test conservées : produit $SYNC_ID ($SYNC_NAME), produit $IDEM_ID ($IDEM_NAME-updated)."
  exit 1
fi

echo
echo "Tous les contrôles ont réussi."
echo "Données de test conservées : produit $SYNC_ID ($SYNC_NAME), produit $IDEM_ID ($IDEM_NAME-updated)."
echo "Journaux : $LOG_DIR"
exit 0
