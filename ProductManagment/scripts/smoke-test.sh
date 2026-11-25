#!/usr/bin/env bash
# Smoke test complet des endpoints ProductsAPI.
# Usage : BASE_URL=http://localhost:5080 ./scripts/smoke-test.sh
set -uo pipefail

BASE_URL="${BASE_URL:-http://localhost:5080}"
PASS=0
FAIL=0

call() {
  local method="$1" url="$2" data="${3:-}"
  local response

  if [ -n "$data" ]; then
    response=$(curl -sS -X "$method" -H "Content-Type: application/json" -d "$data" -w $'\n%{http_code}' "$BASE_URL$url")
  else
    response=$(curl -sS -X "$method" -w $'\n%{http_code}' "$BASE_URL$url")
  fi

  STATUS=$(printf '%s' "$response" | tail -n1)
  BODY=$(printf '%s' "$response" | sed '$d')
}

check() {
  local label="$1" expected="$2" actual="$3"
  if [ "$expected" = "$actual" ]; then
    echo "PASS  $label (obtenu: $actual)"
    PASS=$((PASS + 1))
  else
    echo "FAIL  $label (attendu: $expected, obtenu: $actual)"
    echo "      corps: $BODY"
    FAIL=$((FAIL + 1))
  fi
}

echo "=== 1. Swagger ==="
call GET "/swagger/index.html"
check "GET /swagger/index.html" "200" "$STATUS"

call GET "/swagger/v1/swagger.json"
check "GET /swagger/v1/swagger.json" "200" "$STATUS"
if printf '%s' "$BODY" | grep -q '"/api/Product"'; then
  echo "PASS  swagger.json contient /api/Product"
  PASS=$((PASS + 1))
else
  echo "FAIL  swagger.json ne contient pas /api/Product"
  FAIL=$((FAIL + 1))
fi

echo "=== 2. GET tous les produits ==="
call GET "/api/Product"
check "GET /api/Product" "200" "$STATUS"
COUNT=$(printf '%s' "$BODY" | jq 'length')
check "GET /api/Product retourne 3 produits" "3" "$COUNT"

echo "=== 3. GET par id ==="
call GET "/api/Product/1"
check "GET /api/Product/1" "200" "$STATUS"
NAME=$(printf '%s' "$BODY" | jq -r '.name')
check "Le produit 1 est Laptop" "Laptop" "$NAME"

call GET "/api/Product/99999"
check "GET /api/Product/99999" "404" "$STATUS"

echo "=== 4. POST produit valide ==="
call POST "/api/Product" '{"name":"Smoke Test Product","description":"Temporary product","price":42.50,"stock":3}'
check "POST /api/Product valide" "201" "$STATUS"
NEW_ID=$(printf '%s' "$BODY" | jq -r '.id')
if [ "$NEW_ID" -gt 0 ] 2>/dev/null; then
  echo "PASS  POST retourne un identifiant ($NEW_ID)"
  PASS=$((PASS + 1))
else
  echo "FAIL  POST ne retourne pas d'identifiant"
  FAIL=$((FAIL + 1))
fi

echo "=== 5. POST produit invalide ==="
call POST "/api/Product" '{"name":"Invalid Product","description":"Bad price","price":-10,"stock":1}'
check "POST /api/Product prix négatif" "400" "$STATUS"

call POST "/api/Product" '{"name":"Invalid Product","description":"Zero price","price":0,"stock":1}'
check "POST /api/Product prix nul" "400" "$STATUS"

echo "=== 6. PUT produit existant ==="
call PUT "/api/Product/1" '{"id":1,"name":"Laptop Pro","description":"Updated laptop","price":1800.00,"stock":8}'
check "PUT /api/Product/1" "204" "$STATUS"

call GET "/api/Product/1"
UPDATED_NAME=$(printf '%s' "$BODY" | jq -r '.name')
check "Persistance du PUT (nom)" "Laptop Pro" "$UPDATED_NAME"

echo "=== 7. PUT produit inexistant ==="
call PUT "/api/Product/99999" '{"id":99999,"name":"Ghost","description":null,"price":10,"stock":1}'
check "PUT /api/Product/99999" "404" "$STATUS"

echo "=== 8. DELETE produit temporaire ==="
call DELETE "/api/Product/$NEW_ID"
check "DELETE /api/Product/$NEW_ID" "204" "$STATUS"

call GET "/api/Product/$NEW_ID"
check "GET produit supprimé" "404" "$STATUS"

echo "=== 9. DELETE produit inexistant ==="
call DELETE "/api/Product/99999"
check "DELETE /api/Product/99999" "404" "$STATUS"

echo "=== 10. Restauration de l'état initial ==="
call PUT "/api/Product/1" '{"id":1,"name":"Laptop","description":"High-end gaming laptop","price":1500.00,"stock":10}'
check "PUT /api/Product/1 (restauration)" "204" "$STATUS"

call GET "/api/Product"
check "GET /api/Product final" "200" "$STATUS"
FINAL_COUNT=$(printf '%s' "$BODY" | jq 'length')
check "Nombre final de produits" "3" "$FINAL_COUNT"
FINAL_NAME=$(printf '%s' "$BODY" | jq -r '.[0].name')
check "Produit 1 restauré" "Laptop" "$FINAL_NAME"

echo
echo "Résultat : $PASS PASS, $FAIL FAIL"
[ "$FAIL" -eq 0 ]
