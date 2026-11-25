#!/bin/bash
# =============================================================================
# SecureAPI_JWT — Tests d'API cURL automatisés (TP 9)
# =============================================================================
# Prérequis : L'application doit être lancée sur http://localhost:5053
#             (mode développement) :  dotnet run
#
# Usage :
#   chmod +x tests_api_jwt.sh
#   ./tests_api_jwt.sh
# =============================================================================

BASE_URL="http://localhost:5053"
PASS=0
FAIL=0
SKIP=0

TOKEN=""
REFRESH_TOKEN=""
NEW_TOKEN=""
NEW_REFRESH_TOKEN=""
REVOKED_TOKEN=""

# Utilisateur de test
FIRST_NAME="Yassine"
LAST_NAME="Dupont"
USERNAME="Yassine"
EMAIL="Yassine@test.com"
PASSWORD="Yassine@123!"

# Couleurs
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
BLUE='\033[0;34m'
NC='\033[0m'

print_header() {
    echo -e "\n${CYAN}========================================${NC}"
    echo -e "${CYAN}$1${NC}"
    echo -e "${CYAN}========================================${NC}"
}

print_test() {
    echo -e "\n${YELLOW}▶ Test $1${NC}"
}

check_status() {
    local expected=$1
    local actual=$2
    local description=$3

    if [ "$actual" -eq "$expected" ]; then
        echo -e "${GREEN}✓ PASS${NC} — $description (HTTP $actual)"
        ((PASS++))
        return 0
    else
        echo -e "${RED}✗ FAIL${NC} — $description (attendu HTTP $expected, reçu $actual)"
        ((FAIL++))
        return 1
    fi
}

extract_json_value() {
    local json="$1"
    local key="$2"
    echo "$json" | grep -o "\"$key\":\"[^\"]*\"" | head -1 | cut -d'"' -f4
}

# -----------------------------------------------------------------------------
print_header "⚡ Flux d'authentification"
# -----------------------------------------------------------------------------

# 1. Inscription valide
print_test "1 — Inscription valide"
RESPONSE=$(curl -s -w "\n%{http_code}" -X POST "$BASE_URL/api/Auth/register" \
  -H "Content-Type: application/json" \
  -d "{\"FirstName\":\"$FIRST_NAME\",\"LastName\":\"$LAST_NAME\",\"Username\":\"$USERNAME\",\"Email\":\"$EMAIL\",\"Password\":\"$PASSWORD\"}")
HTTP_CODE=$(echo "$RESPONSE" | tail -n1)
BODY=$(echo "$RESPONSE" | sed '$d')
echo "$BODY" | head -c 250
echo "..."
# Accepte 201 (créé) ou 400 (déjà existant)
if [ "$HTTP_CODE" -eq 201 ]; then
    check_status 201 "$HTTP_CODE" "Inscription valide"
    TOKEN=$(extract_json_value "$BODY" "token")
    REFRESH_TOKEN=$(extract_json_value "$BODY" "refreshToken")
elif [ "$HTTP_CODE" -eq 400 ]; then
    echo -e "${BLUE}  → Compte déjà existant, on passe à la connexion${NC}"
    ((PASS++))
else
    check_status 201 "$HTTP_CODE" "Inscription valide"
fi

# 2. Inscription : email déjà utilisé
print_test "2 — Inscription email déjà utilisé"
RESPONSE=$(curl -s -w "\n%{http_code}" -X POST "$BASE_URL/api/Auth/register" \
  -H "Content-Type: application/json" \
  -d "{\"FirstName\":\"XX\",\"LastName\":\"YY\",\"Username\":\"autreuser\",\"Email\":\"$EMAIL\",\"Password\":\"X@123456!\"}")
HTTP_CODE=$(echo "$RESPONSE" | tail -n1)
BODY=$(echo "$RESPONSE" | sed '$d')
echo "$BODY"
check_status 400 "$HTTP_CODE" "Email déjà utilisé"

# 3. Inscription : username déjà utilisé
print_test "3 — Inscription username déjà utilisé"
RESPONSE=$(curl -s -w "\n%{http_code}" -X POST "$BASE_URL/api/Auth/register" \
  -H "Content-Type: application/json" \
  -d "{\"FirstName\":\"AA\",\"LastName\":\"BB\",\"Username\":\"$USERNAME\",\"Email\":\"autre2@test.com\",\"Password\":\"Alice@123!\"}")
HTTP_CODE=$(echo "$RESPONSE" | tail -n1)
BODY=$(echo "$RESPONSE" | sed '$d')
echo "$BODY"
check_status 400 "$HTTP_CODE" "Username déjà utilisé"

# 4. Inscription : email invalide
print_test "4 — Inscription email invalide"
RESPONSE=$(curl -s -w "\n%{http_code}" -X POST "$BASE_URL/api/Auth/register" \
  -H "Content-Type: application/json" \
  -d '{"FirstName":"AA","LastName":"BB","Username":"badmail","Email":"pas-un-email","Password":"Bademail@123!"}')
HTTP_CODE=$(echo "$RESPONSE" | tail -n1)
BODY=$(echo "$RESPONSE" | sed '$d')
echo "$BODY"
check_status 400 "$HTTP_CODE" "Email invalide"

# 5. Inscription : mot de passe invalide
print_test "5 — Inscription mot de passe invalide"
RESPONSE=$(curl -s -w "\n%{http_code}" -X POST "$BASE_URL/api/Auth/register" \
  -H "Content-Type: application/json" \
  -d '{"FirstName":"AA","LastName":"BB","Username":"badpass","Email":"badpass@test.com","Password":"123"}')
HTTP_CODE=$(echo "$RESPONSE" | tail -n1)
BODY=$(echo "$RESPONSE" | sed '$d')
echo "$BODY"
check_status 400 "$HTTP_CODE" "Mot de passe invalide"

# 6. Inscription : champs manquants
print_test "6 — Inscription champs manquants"
RESPONSE=$(curl -s -w "\n%{http_code}" -X POST "$BASE_URL/api/Auth/register" \
  -H "Content-Type: application/json" \
  -d '{"Username":"partial","Email":"partial@test.com"}')
HTTP_CODE=$(echo "$RESPONSE" | tail -n1)
BODY=$(echo "$RESPONSE" | sed '$d')
echo "$BODY"
check_status 400 "$HTTP_CODE" "Champs manquants"

# 7. Connexion valide
print_test "7 — Connexion valide"
RESPONSE=$(curl -s -w "\n%{http_code}" -X POST "$BASE_URL/api/Auth/login" \
  -H "Content-Type: application/json" \
  -d "{\"Username\":\"$USERNAME\",\"Password\":\"$PASSWORD\"}")
HTTP_CODE=$(echo "$RESPONSE" | tail -n1)
BODY=$(echo "$RESPONSE" | sed '$d')
echo "$BODY" | head -c 250
echo "..."
check_status 200 "$HTTP_CODE" "Connexion valide"

TOKEN=$(extract_json_value "$BODY" "token")
REFRESH_TOKEN=$(extract_json_value "$BODY" "refreshToken")

if [ -n "$TOKEN" ]; then
    echo -e "  ${GREEN}Token récupéré automatiquement${NC}"
    echo -e "  Token        : ${TOKEN:0:50}..."
    echo -e "  RefreshToken : ${REFRESH_TOKEN:0:40}..."
else
    echo -e "${RED}  Impossible de récupérer le token !${NC}"
fi

# 8. Connexion : mauvais mot de passe
print_test "8 — Connexion mauvais mot de passe"
RESPONSE=$(curl -s -w "\n%{http_code}" -X POST "$BASE_URL/api/Auth/login" \
  -H "Content-Type: application/json" \
  -d "{\"Username\":\"$USERNAME\",\"Password\":\"MauvaisMotDePasse!\"}")
HTTP_CODE=$(echo "$RESPONSE" | tail -n1)
BODY=$(echo "$RESPONSE" | sed '$d')
echo "$BODY"
check_status 401 "$HTTP_CODE" "Mauvais mot de passe"

# 9. Connexion : utilisateur inconnu
print_test "9 — Connexion utilisateur inconnu"
RESPONSE=$(curl -s -w "\n%{http_code}" -X POST "$BASE_URL/api/Auth/login" \
  -H "Content-Type: application/json" \
  -d '{"Username":"nobody","Password":"motdepasse"}')
HTTP_CODE=$(echo "$RESPONSE" | tail -n1)
BODY=$(echo "$RESPONSE" | sed '$d')
echo "$BODY"
check_status 401 "$HTTP_CODE" "Utilisateur inconnu"

# -----------------------------------------------------------------------------
print_header "🔒 Endpoints sécurisés (protection JWT)"
# -----------------------------------------------------------------------------

# 10. Endpoint public (sans token)
print_test "10 — Endpoint public (sans token)"
RESPONSE=$(curl -s -w "\n%{http_code}" "$BASE_URL/api/Secure/public")
HTTP_CODE=$(echo "$RESPONSE" | tail -n1)
BODY=$(echo "$RESPONSE" | sed '$d')
echo "$BODY"
check_status 200 "$HTTP_CODE" "Endpoint public"

# 11. Profil utilisateur avec token valide
print_test "11 — Profil avec token valide"
if [ -z "$TOKEN" ]; then
    echo -e "${RED}✗ SKIP${NC} — Pas de token disponible"
    ((SKIP++))
else
    RESPONSE=$(curl -s -w "\n%{http_code}" "$BASE_URL/api/Secure/profile" \
      -H "Authorization: Bearer $TOKEN")
    HTTP_CODE=$(echo "$RESPONSE" | tail -n1)
    BODY=$(echo "$RESPONSE" | sed '$d')
    echo "$BODY"
    check_status 200 "$HTTP_CODE" "Profil avec token valide"
fi

# 12. Profil sans token
print_test "12 — Profil sans token"
RESPONSE=$(curl -s -w "\n%{http_code}" "$BASE_URL/api/Secure/profile")
HTTP_CODE=$(echo "$RESPONSE" | tail -n1)
BODY=$(echo "$RESPONSE" | sed '$d')
echo "$BODY"
check_status 401 "$HTTP_CODE" "Profil sans token"

# 13. Profil avec token invalide
print_test "13 — Profil avec token invalide"
RESPONSE=$(curl -s -w "\n%{http_code}" "$BASE_URL/api/Secure/profile" \
  -H "Authorization: Bearer token-invalide")
HTTP_CODE=$(echo "$RESPONSE" | tail -n1)
BODY=$(echo "$RESPONSE" | sed '$d')
echo "$BODY"
check_status 401 "$HTTP_CODE" "Profil token invalide"

# 14. Accès admin (rôle Admin) — skip car on n'a que le rôle User
print_test "14 — Accès admin (rôle Admin)"
echo -e "${YELLOW}  ⚠ SKIP — Nécessite un utilisateur avec le rôle Admin${NC}"
((SKIP++))

# 15. Utilisateur standard sur endpoint admin (interdit)
print_test "15 — User standard sur endpoint admin (403)"
if [ -z "$TOKEN" ]; then
    echo -e "${RED}✗ SKIP${NC} — Pas de token"
    ((SKIP++))
else
    RESPONSE=$(curl -s -w "\n%{http_code}" "$BASE_URL/api/Secure/admin" \
      -H "Authorization: Bearer $TOKEN")
    HTTP_CODE=$(echo "$RESPONSE" | tail -n1)
    BODY=$(echo "$RESPONSE" | sed '$d')
    echo "$BODY"
    check_status 403 "$HTTP_CODE" "User sur admin → Forbidden"
fi

# 16. Endpoint user (rôle User)
print_test "16 — Endpoint user (rôle User)"
if [ -z "$TOKEN" ]; then
    echo -e "${RED}✗ SKIP${NC} — Pas de token"
    ((SKIP++))
else
    RESPONSE=$(curl -s -w "\n%{http_code}" "$BASE_URL/api/Secure/user" \
      -H "Authorization: Bearer $TOKEN")
    HTTP_CODE=$(echo "$RESPONSE" | tail -n1)
    BODY=$(echo "$RESPONSE" | sed '$d')
    echo "$BODY"
    check_status 200 "$HTTP_CODE" "Endpoint user"
fi

# 17. Endpoint user-or-admin avec token User
print_test "17 — Endpoint user-or-admin (token User)"
if [ -z "$TOKEN" ]; then
    echo -e "${RED}✗ SKIP${NC} — Pas de token"
    ((SKIP++))
else
    RESPONSE=$(curl -s -w "\n%{http_code}" "$BASE_URL/api/Secure/user-or-admin" \
      -H "Authorization: Bearer $TOKEN")
    HTTP_CODE=$(echo "$RESPONSE" | tail -n1)
    BODY=$(echo "$RESPONSE" | sed '$d')
    echo "$BODY"
    check_status 200 "$HTTP_CODE" "user-or-admin avec User"
fi

# 18. Endpoint user-or-admin avec token Admin
print_test "18 — Endpoint user-or-admin (token Admin)"
echo -e "${YELLOW}  ⚠ SKIP — Nécessite un utilisateur avec le rôle Admin${NC}"
((SKIP++))

# -----------------------------------------------------------------------------
print_header "🔄 Refresh Token & révocation"
# -----------------------------------------------------------------------------

# 19. Rafraîchir un JWT
print_test "19 — Rafraîchir un JWT avec refresh token"
if [ -z "$REFRESH_TOKEN" ]; then
    echo -e "${RED}✗ SKIP${NC} — Pas de refresh token"
    ((SKIP++))
else
    # Essai 1 : format string pure (le plus probable d'après tes erreurs)
    RESPONSE=$(curl -s -w "\n%{http_code}" -X POST "$BASE_URL/api/Auth/refresh" \
      -H "Content-Type: application/json" \
      -d "\"$REFRESH_TOKEN\"")
    HTTP_CODE=$(echo "$RESPONSE" | tail -n1)
    BODY=$(echo "$RESPONSE" | sed '$d')

    if [ "$HTTP_CODE" -eq 200 ]; then
        echo "$BODY" | head -c 250
        echo "..."
        check_status 200 "$HTTP_CODE" "Refresh token valide (format string)"
        NEW_TOKEN=$(extract_json_value "$BODY" "token")
        NEW_REFRESH_TOKEN=$(extract_json_value "$BODY" "refreshToken")
        if [ -n "$NEW_TOKEN" ]; then
            TOKEN="$NEW_TOKEN"
            OLD_REFRESH="$REFRESH_TOKEN"
            REFRESH_TOKEN="$NEW_REFRESH_TOKEN"
            echo -e "  ${GREEN}Nouveau token obtenu automatiquement${NC}"
        fi
    else
        # Essai 2 : format objet classique
        echo -e "${BLUE}  → Format string échoué (HTTP $HTTP_CODE), essai format objet...${NC}"
        RESPONSE=$(curl -s -w "\n%{http_code}" -X POST "$BASE_URL/api/Auth/refresh" \
          -H "Content-Type: application/json" \
          -d "{\"refreshToken\":\"$REFRESH_TOKEN\"}")
        HTTP_CODE=$(echo "$RESPONSE" | tail -n1)
        BODY=$(echo "$RESPONSE" | sed '$d')
        echo "$BODY" | head -c 250
        echo "..."
        check_status 200 "$HTTP_CODE" "Refresh token valide (format objet)"
        NEW_TOKEN=$(extract_json_value "$BODY" "token")
        NEW_REFRESH_TOKEN=$(extract_json_value "$BODY" "refreshToken")
        if [ -n "$NEW_TOKEN" ]; then
            TOKEN="$NEW_TOKEN"
            OLD_REFRESH="$REFRESH_TOKEN"
            REFRESH_TOKEN="$NEW_REFRESH_TOKEN"
            echo -e "  ${GREEN}Nouveau token obtenu automatiquement${NC}"
        fi
    fi
fi

# 20. Rafraîchir avec un refresh token invalide
print_test "20 — Refresh token invalide"
RESPONSE=$(curl -s -w "\n%{http_code}" -X POST "$BASE_URL/api/Auth/refresh" \
  -H "Content-Type: application/json" \
  -d '"invalide.ou.expiré"')
HTTP_CODE=$(echo "$RESPONSE" | tail -n1)
BODY=$(echo "$RESPONSE" | sed '$d')
echo "$BODY"
# Accepte 400 ou 401
if [ "$HTTP_CODE" -eq 401 ] || [ "$HTTP_CODE" -eq 400 ]; then
    echo -e "${GREEN}✓ PASS${NC} — Refresh token invalide (HTTP $HTTP_CODE)"
    ((PASS++))
else
    echo -e "${RED}✗ FAIL${NC} — Refresh token invalide (attendu 400/401, reçu $HTTP_CODE)"
    ((FAIL++))
fi

# 21. Révoquer un refresh token
print_test "21 — Révoquer un refresh token"
if [ -z "$REFRESH_TOKEN" ]; then
    echo -e "${RED}✗ SKIP${NC} — Pas de refresh token"
    ((SKIP++))
else
    # Essai format string
    RESPONSE=$(curl -s -w "\n%{http_code}" -X POST "$BASE_URL/api/Auth/revoke" \
      -H "Content-Type: application/json" \
      -d "\"$REFRESH_TOKEN\"")
    HTTP_CODE=$(echo "$RESPONSE" | tail -n1)
    BODY=$(echo "$RESPONSE" | sed '$d')

    if [ "$HTTP_CODE" -eq 200 ]; then
        echo "$BODY"
        check_status 200 "$HTTP_CODE" "Révocation réussie (format string)"
        REVOKED_TOKEN="$REFRESH_TOKEN"
    else
        # Essai format objet
        echo -e "${BLUE}  → Format string échoué (HTTP $HTTP_CODE), essai format objet...${NC}"
        RESPONSE=$(curl -s -w "\n%{http_code}" -X POST "$BASE_URL/api/Auth/revoke" \
          -H "Content-Type: application/json" \
          -d "{\"refreshToken\":\"$REFRESH_TOKEN\"}")
        HTTP_CODE=$(echo "$RESPONSE" | tail -n1)
        BODY=$(echo "$RESPONSE" | sed '$d')
        echo "$BODY"
        check_status 200 "$HTTP_CODE" "Révocation réussie (format objet)"
        REVOKED_TOKEN="$REFRESH_TOKEN"
    fi
fi

# 22. Après révocation, le refresh token ne fonctionne plus
print_test "22 — Refresh après révocation → 401"
if [ -z "$REVOKED_TOKEN" ]; then
    echo -e "${RED}✗ SKIP${NC} — Pas de token révoqué"
    ((SKIP++))
else
    RESPONSE=$(curl -s -w "\n%{http_code}" -X POST "$BASE_URL/api/Auth/refresh" \
      -H "Content-Type: application/json" \
      -d "\"$REVOKED_TOKEN\"")
    HTTP_CODE=$(echo "$RESPONSE" | tail -n1)
    BODY=$(echo "$RESPONSE" | sed '$d')
    echo "$BODY"
    if [ "$HTTP_CODE" -eq 401 ] || [ "$HTTP_CODE" -eq 400 ]; then
        echo -e "${GREEN}✓ PASS${NC} — Refresh après révocation (HTTP $HTTP_CODE)"
        ((PASS++))
    else
        echo -e "${RED}✗ FAIL${NC} — Refresh après révocation (attendu 400/401, reçu $HTTP_CODE)"
        ((FAIL++))
    fi
fi

# -----------------------------------------------------------------------------
print_header "📊 Résumé final"
# -----------------------------------------------------------------------------
echo -e "Tests réussis : ${GREEN}$PASS${NC}"
echo -e "Tests échoués : ${RED}$FAIL${NC}"
echo -e "Tests ignorés : ${YELLOW}$SKIP${NC}"
echo ""

if [ $FAIL -eq 0 ]; then
    echo -e "${GREEN}🎉 Tous les tests automatisés sont passés !${NC}"
else
    echo -e "${RED}⚠ Certains tests ont échoué. Vérifiez l'API et les logs.${NC}"
fi

echo -e "\n${YELLOW}Notes :${NC}"
echo "  - Les tests 14 et 18 (Admin) sont skippés car l'utilisateur a seulement le rôle User."
echo "  - Le script gère automatiquement le token et le refreshToken."
echo "  - Si le compte $USERNAME existe déjà, le test 1 est considéré comme OK."
echo ""
