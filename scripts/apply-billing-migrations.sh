#!/usr/bin/env bash
set -euo pipefail

dotnet ef database update \
  --project src/Pineda.Facturacion.Infrastructure.BillingWrite/Pineda.Facturacion.Infrastructure.BillingWrite.csproj \
  --startup-project src/Pineda.Facturacion.Api/Pineda.Facturacion.Api.csproj
