#!/usr/bin/env bash
# AUTO-PASS SQLite : restaure, compile, teste, crée une base neuve,
# applique la migration, vérifie le seed et exécute le smoke test HTTP.
#
# Usage : ./scripts/auto-validate.sh
# Sortie : 0 si toutes les portes passent, 1 sinon.
set -uo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
export PATH="$DOTNET_ROOT:$DOTNET_ROOT/tools:$PATH"
export ASPNETCORE_ENVIRONMENT="${ASPNETCORE_ENVIRONMENT:-Development}"
export DatabaseProvider="Sqlite"

PORT="${PORT:-5082}"
BASE_URL="http://localhost:$PORT"
TMP_DIR="$(mktemp -d "${TMPDIR:-/tmp}/productmanagement-auto.XXXXXX")"
DB_PATH="$TMP_DIR/validation.db"
API_LOG="$TMP_DIR/api.log"
API_PID=""

PASS=0
FAIL=0
FAILED_STEPS=()

cleanup() {
  if [ -n "$API_PID" ] && kill -0 "$API_PID" 2>/dev/null; then
    kill "$API_PID" 2>/dev/null || true
    wait "$API_PID" 2>/dev/null || true
  fi

  local listeners
  listeners="$(lsof -tiTCP:"$PORT" -sTCP:LISTEN 2>/dev/null || true)"
  if [ -n "$listeners" ]; then
    printf '%s\n' "$listeners" | xargs kill 2>/dev/null || true
  fi

  rm -rf "$TMP_DIR"
}
trap cleanup EXIT

record() {
  local status="$1" label="$2"
  if [ "$status" -eq 0 ]; then
    echo "PASS  $label"
    PASS=$((PASS + 1))
  else
    echo "FAIL  $label"
    FAIL=$((FAIL + 1))
    FAILED_STEPS+=("$label")
  fi
}

run_gate() {
  local label="$1"
  shift
  echo
  echo "--- $label ---"
  "$@"
  record $? "$label"
}

echo "=============================================="
echo " AUTO-PASS SQLite — ProductManagement"
echo "=============================================="

run_gate "1. Restore" dotnet restore ProductManagement.sln
run_gate "2. Build" dotnet build ProductManagement.sln --no-restore
run_gate "3. Tests" dotnet test ProductManagement.sln --no-build

echo
echo "--- 4. Base SQLite neuve + migration automatique ---"
rm -f "$DB_PATH"
export ConnectionStrings__Sqlite="Data Source=$DB_PATH"

dotnet run --project ProductsAPI/ProductsAPI.csproj --no-build --urls "$BASE_URL" \
  > "$API_LOG" 2>&1 &
API_PID=$!

READY=1
for _ in $(seq 1 60); do
  if curl -sf -o /dev/null "$BASE_URL/api/Product"; then
    READY=0
    break
  fi
  if ! kill -0 "$API_PID" 2>/dev/null; then
    break
  fi
  sleep 0.5
done

record "$READY" "4. API démarrée sur base neuve (migration automatique)"

if [ "$READY" -ne 0 ]; then
  echo "--- log API ---"
  tail -30 "$API_LOG"
fi

if [ "$READY" -eq 0 ]; then
  HTTP_STATUS="$(curl -s -o "$TMP_DIR/products.json" -w '%{http_code}' "$BASE_URL/api/Product")"
  PRODUCT_COUNT="$(jq 'length' "$TMP_DIR/products.json" 2>/dev/null || echo 0)"
  [ "$HTTP_STATUS" = "200" ] && [ "$PRODUCT_COUNT" = "3" ]
  record $? "5. GET /api/Product = 200 avec 3 produits"
else
  record 1 "5. GET /api/Product = 200 avec 3 produits"
fi

DB_COUNT="$(sqlite3 "$DB_PATH" 'SELECT COUNT(*) FROM Products;' 2>/dev/null || echo '')"
[ "$DB_COUNT" = "3" ]
record $? "6. Base SQLite : 3 produits seedés"

MIGRATION_COUNT="$(sqlite3 "$DB_PATH" 'SELECT COUNT(*) FROM __EFMigrationsHistory;' 2>/dev/null || echo '')"
[ "$MIGRATION_COUNT" = "1" ]
record $? "7. Migration dbcreate enregistrée"

if [ "$READY" -eq 0 ]; then
  BASE_URL="$BASE_URL" "$ROOT/scripts/smoke-test.sh" > "$TMP_DIR/smoke.log" 2>&1
  SMOKE_STATUS=$?
  tail -3 "$TMP_DIR/smoke.log"
  record "$SMOKE_STATUS" "8. Smoke test HTTP (22 vérifications)"
else
  record 1 "8. Smoke test HTTP (22 vérifications)"
fi

if grep -qE "fail:|Unhandled exception" "$API_LOG"; then
  record 1 "9. Logs API sans erreur"
else
  record 0 "9. Logs API sans erreur"
fi

echo
echo "=============================================="
echo " RÉSULTAT AUTO-PASS : $PASS PASS, $FAIL FAIL"
echo "=============================================="

if [ "$FAIL" -ne 0 ]; then
  printf 'Étapes en échec : %s\n' "${FAILED_STEPS[*]}"
  echo "Corriger puis relancer : ./scripts/auto-validate.sh"
  exit 1
fi

echo "✅ AUTO-PASS SQLITE — toutes les portes sont vertes."
