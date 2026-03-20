#!/usr/bin/env bash
set -euo pipefail

export ASPNETCORE_ENVIRONMENT="${ASPNETCORE_ENVIRONMENT:-Local}"
dotnet run --project src/Pineda.Facturacion.Api/Pineda.Facturacion.Api.csproj
