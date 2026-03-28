# Facturalo Plus quick summary for audit

## Base URLs
- Dev: `https://dev.facturaloplus.com/api/rest/servicio/`
- Prod: `https://app.facturaloplus.com/api/rest/servicio/`

## Operations documented in the guide
### Timbrado
- `timbrar`
- `timbrarTFD`
- `timbrar3`
- `timbrarJSON`
- `timbrarJSON2`
- `timbrarJSON3`
- `timbrarTXT`
- `timbrarTXT2`
- `timbrarTXT3`
- `timbrarConSello`

### Cancelación
- `cancelar2`
- `cancelarPFX2`
- `autorizarCancelacion`

### Consulta
- `consultarEstadoSAT`
- `consultarAutorizacionesPendientes`
- `consultarCfdisRelacionados`
- `consultarCFDI`
- `consultarCreditosDisponibles`

### Validación
- `validar`

## Cancelation motives
- `01` Comprobante emitido con errores con relación
- `02` Comprobante emitido con errores sin relación
- `03` No se llevó a cabo la operación
- `04` Operación nominativa relacionada en una factura global

## Recommended audit questions
1. Which provider operations are actually implemented in the repo?
2. Which ones are exposed through internal API endpoints?
3. Which ones are exposed in UI?
4. Are UUID, XML, provider code, provider message and external status persisted?
5. Is cancellation reason handled correctly, including replacement UUID where applicable?
6. Is there retry or refresh-state support?
7. Is XML recovery supported locally only or also from provider?
