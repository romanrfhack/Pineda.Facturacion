# Correo .com en DEV de Facturación Pineda

Esta herramienta configura **únicamente** `facturas-dev-api.service` en `Sandbox`.
No modifica producción, la base de datos, la aplicación ni los destinatarios.
Requiere Python 3.10+, systemd y ejecución como root en el VPS.

## Configuración

| Parámetro | Valor nuevo |
| --- | --- |
| SMTP | `mail.privateemail.com:587`, STARTTLS con validación de certificado |
| Usuario | `facturacion@autorefaccionespineda.com` |
| Remitente | `facturacion@autorefaccionespineda.com` |
| Contraseña | La del buzón **nuevo**, introducida en la terminal sin eco |
| Destinatario de prueba | El `Email:SafeRecipient` efectivo actual; se conserva |

El proveedor requiere la dirección completa como usuario. Crear el buzón o
cambiar la URL web no cambia las credenciales de la aplicación.
El puerto 587 conserva el modo STARTTLS que usa `System.Net.Mail.SmtpClient`.

## DNS del dominio .com

Revisar el dominio **autorefaccionespineda.com**, no el `.site` ni `dev.`:

| Tipo | Host | Valor |
| --- | --- | --- |
| MX | `@` | `mx1.privateemail.com`, prioridad 10 |
| MX | `@` | `mx2.privateemail.com`, prioridad 10 |
| TXT | `@` | Un único SPF que incluya `include:spf.privateemail.com`, por ejemplo `v=spf1 include:spf.privateemail.com ~all` |
| TXT | `privateemail._domainkey` o el selector mostrado por el proveedor | Clave DKIM del plan **.com**, obtenida desde **Show DKIM** |
| TXT | `_dmarc` | Política DMARC apropiada; para iniciar monitoreo puede ser `v=DMARC1; p=none` |

No copiar la clave DKIM del plan `.site`. No crear un segundo registro SPF si
ya existe uno: se debe integrar el proveedor al registro existente. Namecheap
documenta `privateemail._domainkey` para planes nuevos y `default._domainkey`
para suscripciones anteriores al 2 de junio de 2026; usar el selector que muestre
el panel del plan `.com`. La herramienta consulta ambos.

En Namecheap con BasicDNS/PremiumDNS, **Domain List → Manage → Advanced DNS →
Mail Settings → Private Email** configura los registros básicos; revisar después
DKIM y DMARC en **Host Records**. Con DNS externo, estos cambios corresponden
al proveedor DNS autoritativo. No modificar los registros A/CNAME de la web.

Fuentes del proveedor (consultadas el 2 de noviembre de 2026):

- https://www.namecheap.com/support/knowledgebase/article.aspx/1179/2175/general-private-email-configuration-for-mail-clients-and-mobile-devices/
- https://www.namecheap.com/support/knowledgebase/article.aspx/1338/2176/how-to-set-up-namecheap-private-email-dns-records-for-domains-on-namecheap-basicpremium-nameservers/

## Ejecutar

Copiar `pineda_configurar_correo_com_dev.py` a `/root/` del VPS. Inspección sin
reiniciar servicios ni cambiar configuración:

```bash
python3 /root/pineda_configurar_correo_com_dev.py --inspect
```

Aplicar con una terminal interactiva (`ssh -t` desde otra computadora):

```bash
python3 /root/pineda_configurar_correo_com_dev.py --apply --send-test
```

La herramienta:

1. Comprueba servicio, proceso, ambiente `Sandbox`, directorio de DEV y
   destinatario seguro efectivo. Lee `appsettings`, el entorno del proceso y
   detecta overrides posteriores en UserSecrets/JSON externo. Si encuentra una
   precedencia de correo que no pueda cambiar con seguridad, se detiene.
2. Consulta MX, SPF y DKIM públicos por DNS-over-HTTPS; exige que estén listos.
   Informa DMARC como comprobación adicional. Esto no valida criptográficamente
   una firma DKIM de un mensaje recibido ni sustituye pruebas de entregabilidad.
3. Solicita la contraseña del buzón nuevo sin mostrarla. Valida TLS y login
   SMTP **antes** de cambiar archivos o reiniciar DEV.
4. Respalda sólo los dos archivos administrados bajo
   `/root/pineda-smtp-com-backups/`. Escribe las variables SMTP en
   `/etc/facturas-dev/smtp-com.env` (`root:root`, `0600`) y un drop-in independiente
   `zzzz-pineda-smtp-com.conf`. Conserva los env-files y overrides preexistentes.
   Ningún secreto se guarda en Git, el directorio `publish` o el reporte.
5. Comprueba que su env-file sea el último, reinicia **sólo DEV**, valida
   `/health/live` y `/health/ready`, y verifica en el proceso nuevo usuario,
   contraseña, remitente y la misma configuración `Email` anterior.
6. Con `--send-test`, envía un único mensaje técnico al destinatario seguro
   actual. No contiene documentos de clientes ni usa CC/BCC. Si falla cualquier
   validación tras escribir, restaura automáticamente los archivos anteriores y
   reinicia DEV. Conserva cambios concurrentes detectados en los dos archivos.

Revisión posterior, sin cambiar configuración ni reiniciar:

```bash
python3 /root/pineda_configurar_correo_com_dev.py --verify
```

El JSON sanitizado se guarda en `/root/pineda_correo_com_dev_resultado.json`.
`smtpTestAccepted=true` indica aceptación SMTP, **no recepción en bandeja**.
`applicationEmailVerified` e `inboxDeliveryVerified` permanecen en `false`,
porque deben comprobarse aparte:

- Abrir `https://dev.autorefaccionespineda.com` y enviar un documento existente.
- Confirmar que llega al destinatario de pruebas y que el remitente es `.com`.
- Revisar los encabezados recibidos: SPF/DKIM/DMARC, sin exponer credenciales.

No hay opción de introducir destinatarios distintos desde la línea de comandos.
El script tampoco tiene un modo de producción. Una captura con el buzón en ON
no prueba que DNS, credenciales SMTP o entregas estén funcionando.

## Rollback

Para regresar al estado previo, usar **la ruta exacta `backup` del reporte**:

```bash
python3 /root/pineda_configurar_correo_com_dev.py --rollback /root/pineda-smtp-com-backups/FECHA-ID
```

Valida las huellas de ambos archivos antes de sobrescribir: si alguien los
modificó después, se detiene para evitar perder cambios. No compartir el
contenido de los respaldos: pueden incluir la contraseña SMTP.

## Promoción a main

El 2 de noviembre de 2026, `dev` estaba en `e358555` y `main` en `4cc5571`:
`main` ya contenía todo `dev`, más tres commits (incluido el ajuste de resolución
de configuración para la migración de correos del receptor).

Después de validar DEV, volver a comparar ambas ramas e integrar los cambios
nuevos a `main`. El merge **no promueve los secretos del servidor**, ni copia la
redirección de pruebas a producción: el buzón/usuario/remitente `.com` deben
configurarse por separado para el servicio de producción, conservando su
política de destinatarios reales. Esta herramienta no ejecuta ese paso.

## Pruebas locales

```bash
python3 -m unittest discover -s ops/smtp -p 'test_*.py' -v
```

Las pruebas usan archivos temporales y clientes simulados; no leen configuración
del VPS ni envían mensajes reales.
