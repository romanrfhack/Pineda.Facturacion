# PDF del complemento de pago y entrega por WhatsApp

En `/app/payment-complements?paymentId=75`, la tarjeta de evidencia permite abrir **PDF / WhatsApp** cuando existe timbrado exitoso y UUID. El ID de la petición PDF es el del complemento (`57` en la captura), no el del pago (`75`).

La ventana carga el PDF mediante el endpoint autenticado existente `GET /api/payment-complements/{paymentComplementId}/stamp/pdf`. Permite previsualizarlo y descargarlo como `REP_<UUID>.pdf`; la impresión se realiza abriendo ese archivo.

Para compartir, el archivo ya está preparado antes del clic. Se usa Web Share con archivos cuando está disponible. El usuario elige WhatsApp y el contacto. La alternativa descarga el PDF y abre `wa.me` con texto; **el usuario adjunta el PDF manualmente**. Cerrar el selector no provoca descargas ni abre ventanas. El sistema no confirma la entrega al destinatario ni publica enlaces al PDF. No requiere integración con Meta.

## Representación impresa

- Carta, márgenes de 30 puntos, fondo blanco, azul y dorado en texto y líneas. Se conserva el logotipo configurado cuando está disponible.
- Emisor/receptor, RFC, régimen, código postal, uso CFDI, folio, UUID y fechas literales del XML, sin convertirlas nuevamente de zona horaria.
- Monto total de pagos en MXN separado del total cero del CFDI tipo P / moneda XXX.
- Todos los pagos y documentos relacionados, parcialidades, monedas, equivalencias, saldos e impuestos; también atributos opcionales bancarios y cadenas de pago presentes en el XML.
- QR construido con los datos del XML (incluido Total=0), sellos, certificados y cadena del timbre 1.1. Se reconstruye la cadena desde el timbre cuando están sus atributos completos; se conserva el respaldo persistido para XML históricos incompletos.
- Estado cancelado o cancelación solicitada visible; descarga histórica permitida, botón de compartir omitido para esos estados.
- Continuaciones con UUID, estado y número de página. Valores extensos se dividen sin truncarse.

El formato REP se aísla en un archivo parcial del renderer existente. La representación de facturas de ingreso conserva su diseño. El correo de REP usa automáticamente este mismo formato porque ya comparte el renderer. Se corrigió además la selección del nivel de corrección de errores en el generador QR compartido: los índices L/M/Q/H de las tablas no son los bits de formato 1/0/3/2. Esto mejora también los QR de facturas sin alterar sus datos fiscales. Sin migraciones, cambios de timbrado, modificación de saldos ni cambios de contrato HTTP.

## Validación

Pruebas del renderer y servicios de complementos: fuente XML frente a snapshots discrepantes, UUID incompatible, XML malformado/DTD, fondos blancos, atributos bancarios, paginación, cadena y QR, cancelación, y regresión del PDF de facturas.

Pruebas Angular: descarga y nombre, compartir nativo, alternativa manual, cancelar selector, errores, estados cancelados y liberación de URLs temporales. Flujo E2E simulado: preparar → timbrar → abrir PDF → descargar → cerrar con Escape.

Para validar en DEV: usar un REP ya timbrado; comparar UUID, fechas e importes contra el XML; probar PDF con varias facturas y hojas; abrir el selector en Android/iOS; comprobar descarga/adjunto manual en escritorio. La validación local usa datos simulados; no consulta ni modifica el pago 75 de producción.

Referencias: [SAT: complemento de pagos](https://wwwmat.sat.gob.mx/consultas/92764/comprobante-de-recepcion-de-pagos), [RMF 2026, regla 2.7.1.7](https://sat.gob.mx/minisitio/NormatividadRMFyRGCE/documentos2026/rmf/compiladas/Compilado_Segunda_Modificacion_a_la-Resolucion_Miscelanea_Fiscal_para_2026.pdf), [MDN: navigator.share](https://developer.mozilla.org/en-US/docs/Web/API/Navigator/share).
