#!/usr/bin/env bash
# ---------------------------------------------------------------------------
# Vérification LECTURE SEULE de la propagation ProductsAPI -> OrdersAPI
# et des contrats JSON métier.
#
# Ce script :
#   - ne crée, ne modifie et ne supprime aucune donnée ;
#   - ne démarre et n'arrête aucun service ;
#   - lit les deux bases SQLite en mode immuable ;
#   - interroge les API en GET et RabbitMQ en lecture ;
#   - vérifie les champs métier, pas seulement les codes HTTP.
#
# Code retour : 0 si tous les contrôles passent, 1 sinon.
# ---------------------------------------------------------------------------
set -uo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT_DIR" || exit 1

export PRODUCTS_DB="$ROOT_DIR/ProductsAPI/Products.db"
export ORDERS_DB="$ROOT_DIR/OrdersAPI/Orders.db"
export PRODUCTS_URL="${PRODUCTS_URL:-http://localhost:7066}"
export ORDERS_URL="${ORDERS_URL:-http://localhost:7065}"
export GATEWAY_URL="${GATEWAY_URL:-http://localhost:7162}"
export RABBITMQ_URL="${RABBITMQ_URL:-http://localhost:15672}"

python3 - <<'PY'
import base64
import json
import os
import sqlite3
import sys
import urllib.error
import urllib.parse
import urllib.request

PASS = 0
FAIL = 0
WARN = 0


def check(label, condition, detail=""):
    global PASS, FAIL
    if condition:
        PASS += 1
        print(f"  [PASS] {label}" + (f" ({detail})" if detail else ""))
    else:
        FAIL += 1
        print(f"  [FAIL] {label}" + (f" ({detail})" if detail else ""))


def warn(label, detail=""):
    global WARN
    WARN += 1
    print(f"  [WARN] {label}" + (f" ({detail})" if detail else ""))


def section(title):
    print(f"\n=== {title} ===")


def get_json(url, auth=False):
    request = urllib.request.Request(url, method="GET")
    if auth:
        token = base64.b64encode(b"guest:guest").decode("ascii")
        request.add_header("Authorization", f"Basic {token}")
    with urllib.request.urlopen(request, timeout=15) as response:
        body = response.read().decode("utf-8")
        return response.status, json.loads(body)


def get_status(url):
    request = urllib.request.Request(url, method="GET")
    with urllib.request.urlopen(request, timeout=15) as response:
        response.read()
        return response.status


def ro_connect(path):
    return sqlite3.connect(f"file:{path}?mode=ro&immutable=1", uri=True)


# ---------------------------------------------------------------------------
section("1. Services")
# ---------------------------------------------------------------------------
for label, url in [
    ("ProductsAPI", f"{os.environ['PRODUCTS_URL']}/api/Products"),
    ("OrdersAPI", f"{os.environ['ORDERS_URL']}/api/Orders"),
    ("ApiGateway", f"{os.environ['GATEWAY_URL']}/apigateway/Products"),
]:
    try:
        check(f"{label} répond", get_status(url) == 200, url)
    except Exception as exc:
        check(f"{label} répond", False, str(exc))

# ---------------------------------------------------------------------------
section("2. Propagation SQLite")
# ---------------------------------------------------------------------------
with ro_connect(os.environ["PRODUCTS_DB"]) as products_db:
    products = {
        row[0]: {"name": row[1], "stockQte": row[2], "price": row[3]}
        for row in products_db.execute(
            "SELECT ProductId, Name, StockQte, Price FROM Products;"
        )
    }

with ro_connect(os.environ["ORDERS_DB"]) as orders_db:
    projections = {
        row[0]: row[1]
        for row in orders_db.execute("SELECT Id, Name FROM Products;")
    }
    orders = [
        {
            "ordersId": row[0],
            "customerName": row[1],
            "productId": row[2],
            "orderDate": row[3],
        }
        for row in orders_db.execute(
            "SELECT OrdersId, CustomerName, ProductId, OrderDate FROM Orders ORDER BY OrdersId;"
        )
    ]

common = sorted(set(products) & set(projections))
matching = [product_id for product_id in common if products[product_id]["name"] == projections[product_id]]

check("Au moins un produit commun aux deux bases", bool(common), f"ids={common}")
check(
    "Au moins un produit commun avec le même nom",
    bool(matching),
    f"ids={matching}",
)

if not matching:
    print("\nRésultat : propagation non démontrable sans un produit commun de même nom.")
    print(f"Contrôles réussis : {PASS}")
    print(f"Échecs            : {FAIL}")
    sys.exit(1)

product_id = matching[-1]
product_name = products[product_id]["name"]
projection_name = projections[product_id]
check(
    "Produit sélectionné présent dans Products.db",
    product_id in products,
    f"id={product_id}, name={product_name}",
)
check(
    "Projection présente dans Orders.db",
    product_id in projections,
    f"id={product_id}, name={projection_name}",
)
check(
    "Noms identiques dans les deux bases",
    product_name == projection_name,
    f"{product_name!r} == {projection_name!r}",
)

with ro_connect(os.environ["ORDERS_DB"]) as orders_db:
    projection_count = orders_db.execute(
        "SELECT COUNT(*) FROM Products WHERE Id = ?;", (product_id,)
    ).fetchone()[0]

check(
    "Projection unique (pas de doublon)",
    projection_count == 1,
    f"id={product_id}, lignes={projection_count}",
)

# ---------------------------------------------------------------------------
section("3. Contrats JSON métier")
# ---------------------------------------------------------------------------
direct_status, direct_product = get_json(
    f"{os.environ['PRODUCTS_URL']}/api/Products/{product_id}"
)
check("GET direct produit : HTTP 200", direct_status == 200, f"status={direct_status}")
check(
    "GET direct produit : champs métier",
    set(direct_product) >= {"productId", "name", "stockQte", "price"},
    f"champs={sorted(direct_product)}",
)
check(
    "GET direct produit : identifiant",
    direct_product.get("productId") == product_id,
    f"attendu={product_id}, observé={direct_product.get('productId')}",
)
check(
    "GET direct produit : nom",
    direct_product.get("name") == product_name,
    f"attendu={product_name!r}, observé={direct_product.get('name')!r}",
)
check(
    "GET direct produit : stock",
    direct_product.get("stockQte") == products[product_id]["stockQte"],
    f"attendu={products[product_id]['stockQte']}, observé={direct_product.get('stockQte')}",
)
check(
    "GET direct produit : prix",
    abs(float(direct_product.get("price", -1)) - float(products[product_id]["price"])) < 1e-9,
    f"attendu={products[product_id]['price']}, observé={direct_product.get('price')}",
)

gateway_status, gateway_product = get_json(
    f"{os.environ['GATEWAY_URL']}/apigateway/Products/{product_id}"
)
check("GET Gateway produit : HTTP 200", gateway_status == 200, f"status={gateway_status}")
check(
    "Gateway et API directe renvoient le même produit",
    gateway_product == direct_product,
    f"gateway={gateway_product}",
)

direct_orders_status, direct_orders = get_json(f"{os.environ['ORDERS_URL']}/api/Orders")
gateway_orders_status, gateway_orders = get_json(
    f"{os.environ['GATEWAY_URL']}/apigateway/Orders"
)
check("GET direct commandes : HTTP 200", direct_orders_status == 200)
check("GET Gateway commandes : HTTP 200", gateway_orders_status == 200)
check("Commandes : tableau JSON", isinstance(direct_orders, list), f"type={type(direct_orders).__name__}")
check(
    "Gateway et API directe renvoient les mêmes commandes",
    gateway_orders == direct_orders,
    f"nombre={len(direct_orders)}",
)

for order in direct_orders if isinstance(direct_orders, list) else []:
    order_id = order.get("ordersId")
    check(
        f"Commande {order_id} : champs métier",
        set(order) >= {"ordersId", "orderDate", "customerName", "productId"},
        f"champs={sorted(order)}",
    )
    check(
        f"Commande {order_id} : client non vide",
        isinstance(order.get("customerName"), str) and bool(order["customerName"].strip()),
        f"client={order.get('customerName')!r}",
    )
    if order.get("productId") in projections:
        check(
            f"Commande {order_id} : produit local existant",
            True,
            f"productId={order.get('productId')}",
        )
    else:
        warn(
            f"Commande {order_id} : produit local absent de la projection",
            f"productId={order.get('productId')} ; commande orpheline, aucune contrainte FK dans le PDF",
        )
    check(
        f"Commande {order_id} : date non vide",
        isinstance(order.get("orderDate"), str) and bool(order["orderDate"].strip()),
        f"date={order.get('orderDate')!r}",
    )

# ---------------------------------------------------------------------------
section("4. RabbitMQ")
# ---------------------------------------------------------------------------
try:
    overview_status, overview = get_json(
        f"{os.environ['RABBITMQ_URL']}/api/overview", auth=True
    )
    check("Overview RabbitMQ : HTTP 200", overview_status == 200)
    check(
        "Overview RabbitMQ : produit",
        overview.get("product_name") == "RabbitMQ",
        f"product_name={overview.get('product_name')!r}",
    )
except Exception as exc:
    check("Overview RabbitMQ : accessible", False, str(exc))

exchange_name = "Shared.Messages:ProductCreated"
encoded_exchange = urllib.parse.quote(exchange_name, safe="")
try:
    exchange_status, exchange = get_json(
        f"{os.environ['RABBITMQ_URL']}/api/exchanges/%2F/{encoded_exchange}", auth=True
    )
    check("Exchange ProductCreated : HTTP 200", exchange_status == 200)
    check(
        "Exchange ProductCreated : fanout",
        exchange.get("type") == "fanout",
        f"type={exchange.get('type')!r}",
    )
except Exception as exc:
    check("Exchange ProductCreated : accessible", False, str(exc))

try:
    queue_status, queue = get_json(
        f"{os.environ['RABBITMQ_URL']}/api/queues/%2F/event-listener", auth=True
    )
    check("File event-listener : HTTP 200", queue_status == 200)
    check(
        "File event-listener : consumer actif",
        queue.get("consumers", 0) >= 1,
        f"consumers={queue.get('consumers')}",
    )
    check(
        "File event-listener : aucun message en attente",
        queue.get("messages", -1) == 0,
        f"messages={queue.get('messages')}",
    )
except Exception as exc:
    check("File event-listener : accessible", False, str(exc))

try:
    binding_url = (
        f"{os.environ['RABBITMQ_URL']}/api/bindings/%2F/e/{encoded_exchange}/e/event-listener"
    )
    binding_status, bindings = get_json(binding_url, auth=True)
    check("Binding exchange -> exchange event-listener : HTTP 200", binding_status == 200)
    check(
        "Binding exchange -> exchange event-listener : présent",
        isinstance(bindings, list) and len(bindings) >= 1,
        f"bindings={len(bindings) if isinstance(bindings, list) else bindings}",
    )
except Exception as exc:
    check("Binding exchange -> exchange event-listener : accessible", False, str(exc))

try:
    queue_binding_url = (
        f"{os.environ['RABBITMQ_URL']}/api/bindings/%2F/e/event-listener/q/event-listener"
    )
    queue_binding_status, queue_bindings = get_json(queue_binding_url, auth=True)
    check("Binding exchange event-listener -> queue : HTTP 200", queue_binding_status == 200)
    check(
        "Binding exchange event-listener -> queue : présent",
        isinstance(queue_bindings, list) and len(queue_bindings) >= 1,
        f"bindings={len(queue_bindings) if isinstance(queue_bindings, list) else queue_bindings}",
    )
except Exception as exc:
    check("Binding exchange event-listener -> queue : accessible", False, str(exc))

# ---------------------------------------------------------------------------
print("\n=== Résultat ===")
print(f"Contrôles réussis : {PASS}")
print(f"Avertissements    : {WARN}")
print(f"Échecs            : {FAIL}")
if FAIL == 0 and WARN == 0:
    print("\nPROPAGATION ET CONTRATS JSON : PASS")
    sys.exit(0)
if FAIL == 0:
    print("\nPROPAGATION ET CONTRATS JSON : PASS_WITH_RESERVATIONS")
    sys.exit(0)
print("\nPROPAGATION ET CONTRATS JSON : FAIL")
sys.exit(1)
PY
