# Facturalo Plus vendor docs for CODEX

Use these files as the source of truth for PAC audit and implementation analysis.

## Files
- Guia_de_implementacionREST+.pdf
- FacturaloPlus-API_timbrado-cfdi.sanitized.postman_collection.json
- FacturaloPlus-API_cancelacion-cfdi.sanitized.postman_collection.json

## Sanitization
The original Postman collections contained example values for:
- API keys
- PEM private keys
- PEM certificates
- CSD key/certificate blobs
- PFX blobs
- passwords
- large sample CFDI payloads (XML/JSON/TXT)

Those values were replaced with placeholders so the collections can be safely committed to the repo and indexed by CODEX.

## Suggested repo path
/docs/vendor/facturalo/

## Suggested instruction for AGENTS.md
La documentación del proveedor Facturalo Plus está en /docs/vendor/facturalo.
Usar esos archivos como fuente de verdad para auditoría e implementación fiscal.
No usar secretos reales del repo ni inventar contratos no documentados.

## Quick prompt for CODEX
La documentación del proveedor ya está disponible en /docs/vendor/facturalo.
Usa la guía PDF y las colecciones Postman sanitizadas como fuente de verdad para rehacer la auditoría de cobertura PAC vs repo.
No modifiques código todavía.
