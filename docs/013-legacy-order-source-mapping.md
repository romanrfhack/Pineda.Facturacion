# Legacy Order Source Mapping

## Purpose
Document the exact source tables and columns from the legacy MySQL database that will feed the snapshot import use case.

## Target read model
- LegacyOrderReadModel
- LegacyOrderItemReadModel

## Header mapping
| Target field | Legacy table | Legacy column | Notes |
|---|---|---|---|
| LegacyOrderId | pedidos | noPedido | Primary source identifier for import |
| LegacyOrderNumber | pedidos | refPedido | Human/business reference like A280633 |
| LegacyOrderType | pedidos | TipoPedido | Optional |
| CustomerLegacyId | pedidos | noCliente | |
| CustomerName | clientes | XRazonSocial | For mostrador (
oCliente = 1), XRazonSocial = MOSTRADOR |
| CustomerRfc | clientes | RFC | Optional |
| PaymentCondition | pedidos | condPagoPedido | Example: O |
| PriceListCode | clientes | TipoCliente | Temporary candidate, optional, needs confirmation |
| DeliveryType | pedidos | TipoEntrega | Optional; examples: M, E |
| CurrencyCode | constant | MXN | Legacy sample does not show a currency column yet |
| Subtotal | TBD | TBD | Not directly identified yet |
| DiscountTotal | TBD | TBD | Not directly identified yet |
| TaxTotal | TBD | TBD | Not directly identified yet |
| Total | pedidos | MontoPedido | Current best source for total |

## Detail mapping
| Target field | Legacy table | Legacy column | Notes |
|---|---|---|---|
| LineNumber | TBD | TBD | Not directly identified yet |
| LegacyArticleId | pedidosdet | cveArticulo | |
| Sku | pedidosdet | cveArticulo | Temporary same source as article id, optional |
| Description | articulos | Articulo + Especificacion | Build from available product text; Articulo may be null in some rows |
| UnitCode | pedidosdet | uniMedida | Optional |
| UnitName | articulos | uniMedida | Optional |
| Quantity | pedidosdet | Cantidad | |
| UnitPrice | pedidosdet | SuPrecio | Best current candidate for billing price |
| DiscountAmount | TBD | TBD | Not directly identified yet |
| TaxRate | articulos | PorcentajeIVAArt | Current best source |
| TaxAmount | articulos | IVA | Current best source, needs business confirmation |
| LineTotal | pedidosdet | SuPrecio * Cantidad | Likely calculated if not stored explicitly |
| SatProductServiceCode | TBD | TBD | Not found yet |
| SatUnitCode | TBD | TBD | Not found yet |

## Required joins
- pedidos.noCliente = clientes.noCliente
- pedidosdet.cveArticulo = articulos.cveArticulo
- pedidosdet.cveMarcaArticulo = articulos.cveMarcaArticulo

## Initial import filters
- Exclude rows where pedidos.noCliente = 0
- Exclude rows where pedidos.MontoPedido = 0.00
- Exclude rows where pedidos.refPedido is null
- Import only business-complete orders pending final eligibility rules

## Observed document signals
- pedidos.TipoDocPedido = F appears to represent invoice-oriented orders
- pedidos.TipoDocPedido = N appears to represent nota de venta / mostrador-oriented orders
- This must still be confirmed against business rules

## Open questions
- What column provides a stable line number in pedidosdet?
- Are subtotal, discount, and tax stored explicitly elsewhere or always derived?
- Should clientes.TipoCliente map to PriceListCode, or is there another source?
- Should pedidosdet.Precio or pedidosdet.SuPrecio be the canonical imported unit price?
- Where should SAT product/service code and SAT unit code come from?
- Which legacy status values determine that an order is eligible for billing import?
