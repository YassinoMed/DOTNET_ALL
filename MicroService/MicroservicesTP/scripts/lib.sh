#!/usr/bin/env bash
# ---------------------------------------------------------------------------
# Fonctions partagées par les scripts de démarrage / arrêt / E2E.
# Aucune dépendance à Docker.
# ---------------------------------------------------------------------------

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
LOG_DIR="${LOG_DIR:-$ROOT_DIR/logs}"

PRODUCTS_API_PROJECT="ProductsAPI"
ORDERS_API_PROJECT="OrdersAPI"
GATEWAY_PROJECT="ApiGateway"

PRODUCTS_PORT="${PRODUCTS_PORT:-7066}"
ORDERS_PORT="${ORDERS_PORT:-7065}"
GATEWAY_PORT="${GATEWAY_PORT:-7162}"

RABBITMQ_HOST="${RABBITMQ_HOST:-localhost}"
RABBITMQ_PORT="${RABBITMQ_PORT:-5672}"
RABBITMQ_MANAGEMENT_PORT="${RABBITMQ_MANAGEMENT_PORT:-15672}"
RABBITMQ_SBIN="${RABBITMQ_SBIN:-/opt/homebrew/opt/rabbitmq/sbin}"

mkdir -p "$LOG_DIR"

# PID du processus qui écoute sur un port TCP (vide si personne).
port_pid() {
  lsof -nP -iTCP:"$1" -sTCP:LISTEN -t 2>/dev/null | head -1
}

port_is_open() {
  [[ -n "$(port_pid "$1")" ]]
}

# Attente d'un port TCP : wait_for_port <port> <secondes>
wait_for_port() {
  local port="$1" timeout="${2:-60}" i
  for ((i = 0; i < timeout * 4; i++)); do
    if port_is_open "$port"; then
      return 0
    fi
    sleep 0.25
  done
  return 1
}

# Attente d'une URL HTTP : wait_for_http <url> <secondes>
wait_for_http() {
  local url="$1" timeout="${2:-60}" i code
  for ((i = 0; i < timeout * 4; i++)); do
    code="$(curl -s -o /dev/null -m 2 -w '%{http_code}' "$url" 2>/dev/null || true)"
    if [[ "$code" != "000" && -n "$code" ]]; then
      return 0
    fi
    sleep 0.25
  done
  return 1
}

# Démarrage d'un service en arrière-plan : start_service <projet> <port>
start_service() {
  local project="$1" port="$2"
  local pidfile="$LOG_DIR/$project.pid"
  local logfile="$LOG_DIR/$project.log"

  if port_is_open "$port"; then
    echo "ERREUR : le port $port est déjà utilisé par le PID $(port_pid "$port")." >&2
    return 1
  fi

  (
    cd "$ROOT_DIR/$project" || exit 1
    nohup env \
      ASPNETCORE_ENVIRONMENT=Development \
      ASPNETCORE_URLS="http://localhost:$port" \
      dotnet run --no-launch-profile --no-build \
      > "$logfile" 2>&1 &
    echo $! > "$pidfile"
  )
}

# Arrêt d'un service : stop_service <projet> <port>
stop_service() {
  local project="$1" port="$2"
  local pidfile="$LOG_DIR/$project.pid"

  if [[ -f "$pidfile" ]]; then
    local pid
    pid="$(cat "$pidfile")"
    kill -TERM "$pid" 2>/dev/null || true
    rm -f "$pidfile"
  fi

  # Le processus réellement à l'écoute est un enfant de `dotnet run` :
  # on le termine aussi pour ne pas laisser de port occupé.
  local listener
  listener="$(port_pid "$port")"
  if [[ -n "$listener" ]]; then
    kill -TERM "$listener" 2>/dev/null || true
  fi

  local i
  for ((i = 0; i < 40; i++)); do
    [[ -z "$(port_pid "$port")" ]] && return 0
    sleep 0.25
  done

  listener="$(port_pid "$port")"
  if [[ -n "$listener" ]]; then
    echo "Forçage de l'arrêt du PID $listener sur le port $port." >&2
    kill -KILL "$listener" 2>/dev/null || true
  fi
}

# Vérifie que RabbitMQ répond (TCP) et expose éventuellement l'API de gestion.
rabbitmq_is_up() {
  if [[ -x "$RABBITMQ_SBIN/rabbitmq-diagnostics" ]]; then
    "$RABBITMQ_SBIN/rabbitmq-diagnostics" -q ping >/dev/null 2>&1 && return 0
    return 1
  fi

  (exec 3<>"/dev/tcp/$RABBITMQ_HOST/$RABBITMQ_PORT") 2>/dev/null && exec 3>&- && return 0
  return 1
}

# L'API de gestion est interrogée via /api/queues et non /api/overview :
# avec RabbitMQ 4.4.0 + Erlang/OTP 29, /api/overview plante dans
# rabbit_mgmt_wm_nodes:all_nodes_raw/0 (proplists:get_keys(undefined)) et ne
# renvoie aucune réponse HTTP. Les endpoints queues/connections/exchanges
# restent pleinement fonctionnels. Voir ENVIRONMENT_AUDIT.md.
rabbitmq_management_is_up() {
  local code
  code="$(curl -s -o /dev/null -m 5 -w '%{http_code}' -u guest:guest \
    "http://$RABBITMQ_HOST:$RABBITMQ_MANAGEMENT_PORT/api/queues" 2>/dev/null || true)"
  [[ "$code" == "200" ]]
}

# Valeur scalaire d'une base SQLite : sqlite_scalar <fichier> <requête>
sqlite_scalar() {
  sqlite3 "$1" "$2" 2>/dev/null
}

require_command() {
  local command="$1" hint="${2:-}"
  if ! command -v "$command" >/dev/null 2>&1; then
    echo "ERREUR : commande '$command' introuvable. $hint" >&2
    return 1
  fi
}
