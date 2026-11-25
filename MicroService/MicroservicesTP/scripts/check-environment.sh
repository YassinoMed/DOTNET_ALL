#!/usr/bin/env bash
# ---------------------------------------------------------------------------
# Vérification de l'environnement macOS (aucune modification).
# ---------------------------------------------------------------------------
set -uo pipefail

# shellcheck source=lib.sh
source "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/lib.sh"

echo "=== macOS / matériel ==="
sw_vers 2>/dev/null || true
echo "Architecture : $(uname -m)"

echo
echo "=== .NET ==="
dotnet --version 2>/dev/null || echo "dotnet introuvable"
dotnet --list-sdks 2>/dev/null || true
dotnet --list-runtimes 2>/dev/null | grep -E 'AspNetCore|NETCore' || true

echo
echo "=== Outils ==="
echo "sqlite3 : $(sqlite3 --version 2>/dev/null || echo 'absent')"
echo "curl    : $(curl --version 2>/dev/null | head -1 || echo 'absent')"
echo "Homebrew: $(brew --version 2>/dev/null | head -1 || echo 'absent')"
echo "dotnet-ef local : $(cd "$ROOT_DIR" && dotnet ef --version 2>/dev/null | tail -1 || echo 'non restauré')"

echo
echo "=== RabbitMQ / Erlang ==="
if [[ -x "$RABBITMQ_SBIN/rabbitmqctl" ]]; then
  "$RABBITMQ_SBIN/rabbitmqctl" version 2>/dev/null || true
else
  echo "rabbitmqctl introuvable dans $RABBITMQ_SBIN"
fi
if [[ -x "/opt/homebrew/opt/erlang/bin/erl" ]]; then
  "/opt/homebrew/opt/erlang/bin/erl" -noshell -eval 'erlang:display(erlang:system_info(otp_release)), halt().' 2>/dev/null || true
else
  echo "Erlang introuvable"
fi
rabbitmq_is_up && echo "Broker AMQP : opérationnel ($RABBITMQ_HOST:$RABBITMQ_PORT)" || echo "Broker AMQP : non joignable"
rabbitmq_management_is_up && echo "Management UI : opérationnelle" || echo "Management UI : non joignable"

echo
echo "=== Ports de la solution ==="
for port in "$PRODUCTS_PORT" "$ORDERS_PORT" "$GATEWAY_PORT" "$RABBITMQ_PORT" "$RABBITMQ_MANAGEMENT_PORT"; do
  pid="$(port_pid "$port")"
  if [[ -n "$pid" ]]; then
    echo "Port $port : occupé (PID $pid)"
  else
    echo "Port $port : libre"
  fi
done

echo
echo "=== Bases SQLite ==="
for database in "$ROOT_DIR/ProductsAPI/Products.db" "$ROOT_DIR/OrdersAPI/Orders.db"; do
  if [[ -f "$database" ]]; then
    echo "$database : présente ($(du -h "$database" | cut -f1))"
  else
    echo "$database : absente (lancer les migrations)"
  fi
done

echo
echo "=== IDE ==="
[[ -d "/Applications/Visual Studio Code.app" ]] && echo "VS Code : présent" || echo "VS Code : absent"
[[ -d "/Applications/Visual Studio.app" ]] && echo "Visual Studio 2022 for Mac : présent (retiré du support Microsoft)" || echo "Visual Studio : absent"
[[ -d "/Applications/Postman.app" ]] && echo "Postman : présent" || echo "Postman : absent (utiliser Swagger/curl)"
