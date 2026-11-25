#!/usr/bin/env bash
# ---------------------------------------------------------------------------
# Validation finale en LECTURE SEULE de la livraison MicroservicesTP.
#
# Ce script :
#   - vérifie la solution, les fichiers essentiels et le SDK attendu ;
#   - vérifie l'état des ports sans tuer aucun processus ;
#   - vérifie RabbitMQ sans le redémarrer ;
#   - vérifie l'existence et l'intégrité des deux bases SQLite ;
#   - vérifie la présence des rapports, journaux et résultats de test ;
#   - vérifie l'absence d'opérations destructrices dans les scripts.
#
# Il ne supprime aucun fichier, ne modifie aucune base, ne lance aucune
# migration, ne redémarre aucun service, ne tue aucun processus et
# n'utilise pas Docker.
#
# Code retour : 0 si tous les contrôles obligatoires passent, 1 sinon.
# Les avertissements (WARN) n'affectent pas le code retour.
# ---------------------------------------------------------------------------
set -uo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT_DIR" || exit 1

PRODUCTS_DB="$ROOT_DIR/ProductsAPI/Products.db"
ORDERS_DB="$ROOT_DIR/OrdersAPI/Orders.db"
TRX="$ROOT_DIR/reports/TestResults/complete-tests.trx"

FAILS=0
WARNS=0
PASSES=0

pass() { PASSES=$((PASSES + 1)); printf '  [PASS] %s\n' "$1"; }
warn() { WARNS=$((WARNS + 1)); printf '  [WARN] %s\n' "$1"; }
fail() { FAILS=$((FAILS + 1)); printf '  [FAIL] %s\n' "$1"; }
info() { printf '  [INFO] %s\n' "$1"; }

section() { printf '\n=== %s ===\n' "$1"; }

# ---------------------------------------------------------------------------
section "1. Structure de la solution"
# ---------------------------------------------------------------------------
for path in \
  "MicroservicesTP.sln" \
  "global.json" \
  "Directory.Build.props" \
  "Shared/Shared.csproj" \
  "ProductsAPI/ProductsAPI.csproj" \
  "OrdersAPI/OrdersAPI.csproj" \
  "ApiGateway/ApiGateway.csproj" \
  "Tests/MicroservicesTP.Tests.csproj"; do
  if [[ -f "$path" ]]; then
    pass "Présent : $path"
  else
    fail "Manquant : $path"
  fi
done

for path in scripts/lib.sh scripts/e2e.sh scripts/start-all.sh scripts/stop-all.sh; do
  if [[ -f "$path" ]]; then
    pass "Script présent : $path"
  else
    fail "Script manquant : $path"
  fi
done

# ---------------------------------------------------------------------------
section "2. SDK .NET attendu"
# ---------------------------------------------------------------------------
if command -v dotnet >/dev/null 2>&1; then
  SDK_VERSION="$(dotnet --version 2>/dev/null)"
  EXPECTED_SDK="$(python3 -c 'import json;print(json.load(open("global.json"))["sdk"]["version"])' 2>/dev/null)"
  if [[ -z "$EXPECTED_SDK" ]]; then
    warn "global.json illisible ; SDK observé : ${SDK_VERSION:-inconnu}"
  elif [[ "$SDK_VERSION" == "$EXPECTED_SDK" ]]; then
    pass "SDK .NET $SDK_VERSION conforme à global.json"
  elif [[ "$SDK_VERSION" == 7.0.* ]]; then
    warn "SDK $SDK_VERSION observé, attendu $EXPECTED_SDK (même famille 7.0)"
  else
    fail "SDK $SDK_VERSION incompatible avec l'attendu $EXPECTED_SDK"
  fi

  RID="$(dotnet --info 2>/dev/null | awk -F': *' '/^ RID:/{print $2; exit}')"
  info "RID : ${RID:-inconnu}"
else
  fail "Commande dotnet introuvable"
fi

# ---------------------------------------------------------------------------
section "3. Ports (aucun processus n'est arrêté)"
# ---------------------------------------------------------------------------
port_pid() {
  lsof -nP -iTCP:"$1" -sTCP:LISTEN -t 2>/dev/null | head -1
}

for port in 7066 7065 7162; do
  pid="$(port_pid "$port")"
  if [[ -z "$pid" ]]; then
    pass "Port $port libre"
  else
    warn "Port $port occupé par PID $pid ($(ps -o comm= -p "$pid" 2>/dev/null))"
  fi
done

for port in 5672 15672; do
  pid="$(port_pid "$port")"
  if [[ -n "$pid" ]]; then
    pass "Port RabbitMQ $port en écoute (PID $pid)"
  else
    fail "Port RabbitMQ $port non en écoute"
  fi
done

# ---------------------------------------------------------------------------
section "4. RabbitMQ (sans redémarrage)"
# ---------------------------------------------------------------------------
RABBITMQ_SBIN="${RABBITMQ_SBIN:-/opt/homebrew/opt/rabbitmq/sbin}"
if command -v rabbitmq-diagnostics >/dev/null 2>&1; then
  if rabbitmq-diagnostics -q ping >/dev/null 2>&1; then
    pass "rabbitmq-diagnostics ping réussi"
  else
    fail "rabbitmq-diagnostics ping en échec"
  fi
elif [[ -x "$RABBITMQ_SBIN/rabbitmq-diagnostics" ]]; then
  if "$RABBITMQ_SBIN/rabbitmq-diagnostics" -q ping >/dev/null 2>&1; then
    pass "rabbitmq-diagnostics ping réussi"
  else
    fail "rabbitmq-diagnostics ping en échec"
  fi
else
  warn "rabbitmq-diagnostics introuvable ; contrôle via l'API de gestion uniquement"
fi

MGMT_CODE="$(curl -s -m 8 -o /dev/null -w '%{http_code}' -u guest:guest \
  http://localhost:15672/api/queues 2>/dev/null)"
if [[ "$MGMT_CODE" == "200" ]]; then
  pass "API de gestion RabbitMQ /api/queues accessible (HTTP 200)"
else
  fail "API de gestion RabbitMQ /api/queues inaccessible (HTTP ${MGMT_CODE:-000})"
fi

# Note : /api/overview est affecté par le bug RabbitMQ 4.4.0 + OTP 29.
OVERVIEW_CODE="$(curl -s -m 5 -o /dev/null -w '%{http_code}' -u guest:guest \
  http://localhost:15672/api/overview 2>/dev/null)"
if [[ "$OVERVIEW_CODE" == "200" ]]; then
  pass "Page /api/overview accessible (HTTP 200)"
else
  warn "Page /api/overview en échec (HTTP ${OVERVIEW_CODE:-000}) — bug environnemental documenté, sans impact applicatif"
fi

# ---------------------------------------------------------------------------
section "5. Bases SQLite (lecture seule, mode immutable)"
# ---------------------------------------------------------------------------
sqlite_ro() {
  # Ouvre la base sans écriture possible : mode=ro + immutable=1.
  sqlite3 "file:$1?mode=ro&immutable=1" "$2" 2>/dev/null
}

for db in "$PRODUCTS_DB" "$ORDERS_DB"; do
  if [[ -f "$db" ]]; then
    pass "Base présente : ${db#"$ROOT_DIR"/} ($(stat -f%z "$db" 2>/dev/null) octets)"
  else
    fail "Base manquante : ${db#"$ROOT_DIR"/}"
  fi
done

if [[ -f "$PRODUCTS_DB" ]]; then
  INTEGRITY="$(sqlite_ro "$PRODUCTS_DB" "PRAGMA integrity_check;")"
  if [[ "$INTEGRITY" == "ok" ]]; then
    pass "Products.db : PRAGMA integrity_check = ok"
  else
    fail "Products.db : integrity_check = ${INTEGRITY:-échec de lecture}"
  fi

  if [[ "$(sqlite_ro "$PRODUCTS_DB" "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='Products';")" == "1" ]]; then
    pass "Products.db : table Products présente"
    info "Products.db : $(sqlite_ro "$PRODUCTS_DB" "SELECT COUNT(*) FROM Products;") produit(s)"
  else
    fail "Products.db : table Products absente"
  fi

  MIG_PROD="$(sqlite_ro "$PRODUCTS_DB" "SELECT COUNT(*) FROM __EFMigrationsHistory;")"
  if [[ "$MIG_PROD" =~ ^[0-9]+$ ]] && [[ "$MIG_PROD" -ge 1 ]]; then
    pass "Products.db : historique EF présent ($MIG_PROD migration(s))"
  else
    fail "Products.db : historique EF absent ou illisible"
  fi
fi

if [[ -f "$ORDERS_DB" ]]; then
  INTEGRITY="$(sqlite_ro "$ORDERS_DB" "PRAGMA integrity_check;")"
  if [[ "$INTEGRITY" == "ok" ]]; then
    pass "Orders.db : PRAGMA integrity_check = ok"
  else
    fail "Orders.db : integrity_check = ${INTEGRITY:-échec de lecture}"
  fi

  for table in Orders Products; do
    if [[ "$(sqlite_ro "$ORDERS_DB" "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='$table';")" == "1" ]]; then
      pass "Orders.db : table $table présente"
    else
      fail "Orders.db : table $table absente"
    fi
  done

  info "Orders.db : $(sqlite_ro "$ORDERS_DB" "SELECT COUNT(*) FROM Products;") projection(s) produit, $(sqlite_ro "$ORDERS_DB" "SELECT COUNT(*) FROM Orders;") commande(s)"

  MIG_ORD="$(sqlite_ro "$ORDERS_DB" "SELECT COUNT(*) FROM __EFMigrationsHistory;")"
  if [[ "$MIG_ORD" =~ ^[0-9]+$ ]] && [[ "$MIG_ORD" -ge 1 ]]; then
    pass "Orders.db : historique EF présent ($MIG_ORD migration(s))"
  else
    fail "Orders.db : historique EF absent ou illisible"
  fi
fi

# Isolation des chaînes de connexion.
if grep -q 'Data Source=Products.db' ProductsAPI/appsettings.json 2>/dev/null \
  && ! grep -q 'Data Source=Orders.db' ProductsAPI/appsettings.json 2>/dev/null; then
  pass "ProductsAPI pointe uniquement vers Products.db"
else
  fail "ProductsAPI : chaîne de connexion absente ou ambiguë"
fi

if grep -q 'Data Source=Orders.db' OrdersAPI/appsettings.json 2>/dev/null \
  && ! grep -q 'Data Source=Products.db' OrdersAPI/appsettings.json 2>/dev/null; then
  pass "OrdersAPI pointe uniquement vers Orders.db"
else
  fail "OrdersAPI : chaîne de connexion absente ou ambiguë"
fi

if grep -Rqs 'Data Source=Products.db' OrdersAPI --include='*.cs' --include='*.json' \
  --exclude-dir=bin --exclude-dir=obj; then
  fail "OrdersAPI référence Products.db (violation d'isolation)"
else
  pass "Aucune référence à Products.db dans OrdersAPI"
fi

# ---------------------------------------------------------------------------
section "6. Rapports, journaux et documentation"
# ---------------------------------------------------------------------------
for path in \
  "FINAL_AUDIT_BASELINE.md" \
  "RAPPORT_FINAL_CONFORMITE.md" \
  "README.md" \
  "TEST_RESULTS.md" \
  "docs/ARCHITECTURE_EVOLUTION.md" \
  "docs/AUTO_PASS.md" \
  "docs/CONFORMITE_PDF.md" \
  "docs/GUIDE_DEMARRAGE.md" \
  "docs/INSTALLATION_MACOS.md" \
  "reports/BUILD_REPORT.md" \
  "reports/UNIT_TEST_REPORT.md" \
  "reports/TEST_COVERAGE_AUDIT.md" \
  "reports/E2E_FINAL_REPORT.md" \
  "reports/AUTO_PASS_REPORT.md" \
  "reports/INDEX_FINAL.md" \
  "logs/build-final.log" \
  "logs/tests-final.log" \
  "logs/e2e-final.log" \
  "reports/TestResults/complete-tests.trx"; do
  if [[ -s "$path" ]]; then
    pass "Livrable présent : $path"
  else
    fail "Livrable manquant ou vide : $path"
  fi
done

# ---------------------------------------------------------------------------
section "7. Preuves de build"
# ---------------------------------------------------------------------------
if [[ -f logs/build-final.log ]]; then
  if grep -q '0 Erreur(s)' logs/build-final.log && grep -q '0 Avertissement(s)' logs/build-final.log; then
    pass "Build final : 0 erreur / 0 avertissement"
  else
    fail "Build final : erreurs ou avertissements détectés dans le journal"
  fi

  if grep -q 'BUILD_EXIT=0' logs/build-final.log; then
    pass "Build final : code retour 0 consigné"
  else
    fail "Build final : code retour 0 absent du journal"
  fi
fi

# ---------------------------------------------------------------------------
section "8. Résultats des tests xUnit (TRX)"
# ---------------------------------------------------------------------------
if [[ -s "$TRX" ]]; then
  TRX_COUNTS="$(python3 - "$TRX" <<'PY'
import sys
import xml.etree.ElementTree as ET

path = sys.argv[1]
ns = {'t': 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
try:
    root = ET.parse(path).getroot()
    counters = root.find('.//t:ResultSummary/t:Counters', ns)
    outcomes = {}
    for result in root.findall('.//t:UnitTestResult', ns):
        outcome = result.get('outcome', 'Inconnu')
        outcomes[outcome] = outcomes.get(outcome, 0) + 1
    total = int(counters.get('total', 0)) if counters is not None else sum(outcomes.values())
    passed = int(counters.get('passed', 0)) if counters is not None else outcomes.get('Passed', 0)
    failed = int(counters.get('failed', 0)) if counters is not None else outcomes.get('Failed', 0)
    skipped = outcomes.get('NotExecuted', 0)
    print(f"{total} {passed} {failed} {skipped}")
except Exception as exc:  # noqa: BLE001
    print(f"ERREUR {exc}")
PY
)"

  read -r TRX_TOTAL TRX_PASSED TRX_FAILED TRX_SKIPPED <<<"$TRX_COUNTS"
  if [[ "${TRX_TOTAL:-ERREUR}" =~ ^[0-9]+$ ]]; then
    info "TRX : total=$TRX_TOTAL, réussis=$TRX_PASSED, échecs=$TRX_FAILED, ignorés=$TRX_SKIPPED"
    if [[ "$TRX_FAILED" -eq 0 ]]; then
      pass "Aucun test xUnit en échec"
    else
      fail "$TRX_FAILED test(s) xUnit en échec"
    fi
    if [[ "$TRX_PASSED" -ge 46 ]]; then
      pass "Au moins 46 tests xUnit réussis"
    else
      fail "Seulement $TRX_PASSED test(s) réussi(s), attendu >= 46"
    fi
  else
    fail "TRX illisible : $TRX_COUNTS"
  fi
else
  fail "Résultats TRX absents : reports/TestResults/complete-tests.trx"
fi

# ---------------------------------------------------------------------------
section "9. Preuves E2E"
# ---------------------------------------------------------------------------
if [[ -f logs/e2e-final.log ]]; then
  E2E_PASS="$(grep -oE 'Checks PASS : [0-9]+' logs/e2e-final.log | tail -1 | grep -oE '[0-9]+')"
  E2E_FAIL="$(grep -oE 'Checks FAIL : [0-9]+' logs/e2e-final.log | tail -1 | grep -oE '[0-9]+')"
  if [[ "${E2E_FAIL:-1}" -eq 0 ]]; then
    pass "E2E final : 0 échec consigné"
  else
    fail "E2E final : échecs consignés (FAIL=${E2E_FAIL:-inconnu})"
  fi
  if [[ "${E2E_PASS:-0}" -ge 66 ]]; then
    pass "E2E final : $E2E_PASS contrôles réussis (attendu >= 66)"
  else
    fail "E2E final : seulement ${E2E_PASS:-0} contrôle(s) réussi(s)"
  fi
fi

# ---------------------------------------------------------------------------
section "10. Absence d'opérations destructrices dans les scripts"
# ---------------------------------------------------------------------------
if grep -RqsE --exclude='validate-final.sh' 'dotnet ef database drop|EnsureDeleted|rabbitmqctl reset|docker[ -]compose|docker run' scripts/; then
  fail "Opération destructive ou Docker détecté dans scripts/"
else
  pass "Aucune commande destructive ni Docker dans scripts/"
fi

if grep -RqsE 'rm[[:space:]]+-[a-zA-Z]*f?[[:space:]]+[^|;]*\.db' scripts/; then
  fail "Suppression de fichier .db détectée dans scripts/"
else
  pass "Aucune suppression de base .db dans scripts/"
fi

# ---------------------------------------------------------------------------
section "Résultat"
# ---------------------------------------------------------------------------
printf 'Contrôles réussis : %d\n' "$PASSES"
printf 'Avertissements    : %d\n' "$WARNS"
printf 'Échecs            : %d\n' "$FAILS"

if [[ "$FAILS" -eq 0 ]]; then
  printf '\nVALIDATION FINALE : PASS\n'
  exit 0
else
  printf '\nVALIDATION FINALE : FAIL (%d contrôle(s) obligatoire(s) en échec)\n' "$FAILS"
  exit 1
fi
