#!/usr/bin/env bash
# Lance l'API en développement sur http://localhost:5080.
set -euo pipefail

cd "$(dirname "$0")/.."

export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
export PATH="$DOTNET_ROOT:$DOTNET_ROOT/tools:$PATH"
export ASPNETCORE_ENVIRONMENT="${ASPNETCORE_ENVIRONMENT:-Development}"

if ! dotnet --version >/dev/null 2>&1; then
  echo "Le SDK .NET est introuvable. Installez .NET 8 ou vérifiez DOTNET_ROOT." >&2
  exit 1
fi

dotnet tool restore >/dev/null
dotnet run --project ProductsAPI/ProductsAPI.csproj --urls "http://localhost:5080"
