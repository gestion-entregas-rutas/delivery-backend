# Implementación de PayPal en otro proyecto

Guía basada en el flujo PayPal Orders v2 implementado en Pacific Nexus Airways. Está pensada para adaptar el mismo patrón a otra API, especialmente ASP.NET Core, y a un cliente web o Flutter.

> **Credencial expuesta:** en la conversación anterior se compartió un `ClientSecret` de Sandbox. Rótalo desde PayPal Developer Dashboard antes de volver a usarlo. No copies secretos desde el historial del chat ni los incluyas en este archivo, Git, Flutter, Swagger o solicitudes HTTP.

## 1. Qué reutilizar de la cuenta PayPal

Si ambos proyectos cobrarán a la misma cuenta vendedora y usarán Sandbox, pueden pertenecer a la misma cuenta Business Sandbox. Para aislar cada integración, es preferible crear una aplicación REST Sandbox separada para el segundo proyecto.

- **Client ID y Client Secret:** se generan para una aplicación REST y un entorno concretos. El backend los usa para autenticarse con PayPal. Si se conserva la aplicación existente, actualiza el secreto después de rotarlo.
- **Merchant ID:** identifica al vendedor. Se puede usar el mismo cuando la cuenta Business Sandbox sea la misma.
- **Return URL y Cancel URL:** deben apuntar al flujo de retorno/cancelación del nuevo cliente. No deben conservar las rutas del proyecto anterior si la app nueva tiene otras pantallas o dominio.
- **Webhook ID:** se genera al registrar una URL de webhook para una aplicación. Solo se necesita si se implementarán webhooks y su verificación.

Los valores de `ReturnUrl`, `CancelUrl` y `WebhookId` suelen ser distintos por integración. No existe un `notepad` ni una ruta de archivo como clave de configuración: el identificador `UserSecretsId` es propio de cada proyecto .NET.

## 2. Configuración segura

Use estas claves en User Secrets en desarrollo y en variables de entorno o un gestor de secretos al desplegar. En .NET, los dos puntos se escriben como doble guion bajo en variables de entorno.

| Clave .NET | Variable de entorno | Uso |
|---|---|---|
| `PayPal:Habilitado` | `PayPal__Habilitado` | Habilita la integración. |
| `PayPal:Entorno` | `PayPal__Entorno` | `Sandbox` para pruebas; `Live` requiere soporte explícito en el código y credenciales reales. |
| `PayPal:ClientId` | `PayPal__ClientId` | Identifica la aplicación PayPal. |
| `PayPal:ClientSecret` | `PayPal__ClientSecret` | Secreto servidor a servidor. Nunca se envía al cliente. |
| `PayPal:MerchantId` | `PayPal__MerchantId` | Identifica la cuenta vendedora que recibirá el pago. |
| `PayPal:ReturnUrl` | `PayPal__ReturnUrl` | Pantalla/ruta a la que PayPal devuelve al comprador tras aprobar. |
| `PayPal:CancelUrl` | `PayPal__CancelUrl` | Pantalla/ruta a la que PayPal devuelve al comprador al cancelar. |
| `PayPal:WebhookId` | `PayPal__WebhookId` | ID del webhook, solo si se reciben y verifican eventos. |

Ejemplo de claves sin valores reales:

```text
PayPal:Habilitado=true
PayPal:Entorno=Sandbox
PayPal:ClientId=<CLIENT_ID_SANDBOX>
PayPal:ClientSecret=<CLIENT_SECRET_SANDBOX_ROTADO>
PayPal:MerchantId=<MERCHANT_ID_BUSINESS_SANDBOX>
PayPal:ReturnUrl=<URL_DE_RETORNO_DEL_NUEVO_CLIENTE>
PayPal:CancelUrl=<URL_DE_CANCELACION_DEL_NUEVO_CLIENTE>
PayPal:WebhookId=<SOLO_SI_SE_CONFIGURA_WEBHOOK>
```

Inicializa User Secrets para el `.csproj` nuevo y registra allí los valores localmente:

```powershell
dotnet user-secrets init --project .\OtroBackend.csproj
dotnet user-secrets set "PayPal:Habilitado" "true" --project .\OtroBackend.csproj
dotnet user-secrets set "PayPal:Entorno" "Sandbox" --project .\OtroBackend.csproj
dotnet user-secrets set "PayPal:ClientId" "<CLIENT_ID_SANDBOX>" --project .\OtroBackend.csproj
dotnet user-secrets set "PayPal:ClientSecret" "<CLIENT_SECRET_SANDBOX_ROTADO>" --project .\OtroBackend.csproj
dotnet user-secrets set "PayPal:MerchantId" "<MERCHANT_ID_BUSINESS_SANDBOX>" --project .\OtroBackend.csproj
dotnet user-secrets set "PayPal:ReturnUrl" "<URL_DE_RETORNO>" --project .\OtroBackend.csproj
dotnet user-secrets set "PayPal:CancelUrl" "<URL_DE_CANCELACION>" --project .\OtroBackend.csproj
```

Usa una cuenta compradora **Personal Sandbox** distinta de la cuenta Business vendedora. Las credenciales Sandbox y Live son diferentes. La API REST obtiene un token OAuth intercambiando el Client ID y el Client Secret en el endpoint correspondiente al entorno.[Autenticación REST de PayPal](https://developer.paypal.com/api/rest/authentication)

## 3. Componentes recomendados en la API

Separa responsabilidades como en este backend:

1. **Opciones y validación**: lee la sección `PayPal`, comprueba entorno, credenciales, Merchant ID y URLs. No permitas llamadas al proveedor si la configuración no está completa.
2. **Cliente PayPal**: clase con `HttpClient` para obtener token OAuth, crear/consultar/capturar órdenes y, opcionalmente, verificar webhooks.
3. **Servicio de pagos**: aplica reglas del negocio, valida que la reserva pertenezca al usuario, guarda los estados y concilia las respuestas.
4. **Repositorio y persistencia**: vincula pago, reserva, IDs de orden/captura, moneda, total, entorno y estado. Define unicidad para evitar más de un pago accidental por reserva y duplicados de orden/captura.
5. **Controlador**: expone operaciones propias del usuario, conciliación administrativa y webhook público si se configura.

El proyecto actual usa `HttpClient` y `System.Text.Json`; no requiere un SDK específico de PayPal para este flujo.[Referencia de Orders v2](https://developer.paypal.com/api/orders/v2/)

## 4. Flujo de compra

### Paso A: crear una reserva

La API calcula el precio, impuestos/descuentos y el total a cobrar. Devuelve el ID de reserva y su versión de concurrencia (`rowVersion`). El cliente conserva esos valores y no calcula ni reemplaza el monto del pago.

### Paso B: crear orden PayPal

El cliente autenticado llama a un endpoint propio, por ejemplo:

```http
POST /api/reservas/{reservaId}/paypal/orden
Authorization: Bearer <token-de-sesion>
Content-Type: application/json

{"rowVersion":"<ROW_VERSION_DEVUELTA_POR_LA_API>"}
```

La API valida propietario, estado, expiración, asientos y versión vigente; crea y persiste un pago pendiente; luego obtiene el token OAuth y llama a `POST /v2/checkout/orders` con:

- `intent: CAPTURE`;
- `currency_code: USD` y el total calculado/guardado por el backend;
- `payee.merchant_id` del vendedor esperado;
- identificadores internos en `reference_id`, `custom_id` e `invoice_id` para conciliación;
- `return_url` y `cancel_url` del nuevo frontend;
- `user_action: PAY_NOW` y `shipping_preference: NO_SHIPPING` para un producto digital como un boleto.

PayPal responde con el ID de orden y un enlace de aprobación. La API devuelve ese enlace al cliente, que debe abrirlo en navegador o Custom Tab. PayPal documenta el patrón de crear la orden y después capturarla con Orders v2.[Crear/capturar órdenes](https://developer.paypal.com/api/orders/v2/)

### Paso C: aprobación y retorno

El comprador aprueba en PayPal y vuelve a `ReturnUrl`. Esa redirección **no prueba el pago**. El frontend debe retomar el ID de la reserva y la sesión autenticada; nunca coloque el token de sesión en la URL.

En Flutter móvil, use un mecanismo apropiado para volver a la app, como App Links/Universal Links o un esquema de enlace configurado. En Flutter Web, use una ruta del propio frontend. La URL de retorno no debe ser una página de login ni la URL de Swagger.

### Paso D: capturar y verificar

Tras volver, el cliente llama a su API autenticado:

```http
POST /api/reservas/{reservaId}/paypal/captura
Authorization: Bearer <token-de-sesion>
```

No envía monto, captura ni una orden elegida por el cliente. El backend carga el ID de orden almacenado, consulta su estado, verifica identidad, monto, moneda y vendedor; si está aprobada, solicita la captura y vuelve a leer el resultado desde PayPal.

Solo se acredita cuando la captura verificada aparece completada. Un estado de orden `APPROVED` por sí mismo no significa que el dinero haya sido capturado; PayPal recomienda verificar también el estado de la captura.[Estados de órdenes](https://developer.paypal.com/api/orders/v2/definitions/order_status/)

### Paso E: mostrar estado

El cliente consulta:

```http
GET /api/reservas/{reservaId}/paypal
Authorization: Bearer <token-de-sesion>
```

Muestra éxito solo si la respuesta del backend indica pago completado y reserva pagada. Si el estado es `pendiente`, `verificando` o `revision`, muestra que el pago está en comprobación y consulta nuevamente más tarde. No liberes asientos ni inicies otro cobro desde el cliente ante una respuesta incierta.

## 5. Endpoints de referencia del proyecto actual

| Método y ruta | Quién | Propósito |
|---|---|---|
| `POST /api/reservas/{id}/paypal/orden` | Propietario autenticado | Crea o recupera la orden PayPal para el total de la reserva. |
| `POST /api/reservas/{id}/paypal/captura` | Propietario autenticado | Captura la orden aprobada asociada a la reserva. |
| `GET /api/reservas/{id}/paypal` | Propietario autenticado | Consulta y concilia el estado propio. |
| `POST /api/admin/pagos/paypal/{pagoId}/conciliacion` | Administrador | Fuerza conciliación de un pago. |
| `POST /api/pagos/paypal/webhook` | PayPal, sin token de usuario | Recibe eventos verificados, si se configura un webhook. |
| `GET /api/admin/ventas/resumen?entorno=Sandbox` | Administrador | Consulta importes agregados del entorno indicado. |

El endpoint de compra simulada fue retirado en este proyecto. El flujo real de pago crea una orden PayPal y la captura desde el backend.

## 6. Estados inciertos, reintentos e idempotencia

Una caída de red puede ocurrir después de que PayPal haya procesado una solicitud, pero antes de que la API reciba la respuesta. Para evitar cobrar dos veces:

- Persiste el registro del pago y el intento de operación antes de enviar una captura.
- Usa `PayPal-Request-Id` estable y distinto para crear y capturar; el ID debe conservarse al reintentar la misma operación.
- Trata el timeout de captura como resultado incierto. Consulta la orden/captura o concilia antes de reintentar.
- Conserva temporalmente la reserva/asientos mientras el resultado siga incierto. Libéralos solo con evidencia de que no hubo captura o por una política explícita y segura.
- No permitas cambiar el total/asientos cuando ya exista una orden asociada.
- Registra y verifica IDs únicos de orden, captura y evento webhook.

En el backend actual, un worker de conciliación consulta pagos protegidos periódicamente; también puede conciliarse mediante el GET propio o el endpoint administrativo. Los estados inciertos permanecen en revisión sin lanzar otro cobro automáticamente.

## 7. Webhooks: opcionales para el flujo básico, útiles para conciliación

El flujo de creación, aprobación, captura y consulta puede funcionar sin `WebhookId`. Si se añade webhook:

1. Publica un endpoint HTTPS accesible desde PayPal.
2. Regístralo en la aplicación PayPal y guarda el ID asociado a esa URL.
3. Selecciona eventos necesarios, por ejemplo `CHECKOUT.ORDER.APPROVED`, `PAYMENT.CAPTURE.COMPLETED`, `PAYMENT.CAPTURE.PENDING`, `PAYMENT.CAPTURE.DENIED`, `PAYMENT.CAPTURE.REFUNDED` y `PAYMENT.CAPTURE.REVERSED`.
4. Verifica la firma antes de cambiar cualquier estado. Se puede usar el endpoint oficial `verify-webhook-signature` con el `WebhookId` registrado.
5. Deduplica por ID de evento y responde 2xx cuando la entrega se haya procesado de manera segura.

PayPal asigna un Webhook ID al registrar la URL; los mensajes deben verificarse antes de procesarse.[Integración de webhooks](https://developer.paypal.com/api/rest/webhooks/rest/) y [verificación de firma](https://developer.paypal.com/api/webhooks/v1/)

## 8. Pruebas en Sandbox

Prueba en este orden, primero en local y después en el hosting de desarrollo:

1. Comprueba que el backend valida la configuración y obtiene un token OAuth.
2. Crea una reserva y guarda el `reservaId` y `rowVersion`.
3. Crea la orden y comprueba que el monto USD coincide con el total de la reserva.
4. Abre `urlAprobacion` e inicia sesión con un comprador Personal Sandbox.
5. Aprueba el pago y confirma que PayPal retorna a la ruta del frontend adecuada.
6. Llama a captura autenticada y verifica el estado consultándolo desde la API.
7. Revisa en base de datos orden, captura, monto, moneda, entorno y estados.
8. Prueba cancelación, doble envío de captura, timeout, orden no aprobada, reserva vencida y eventos duplicados.
9. Si se configuró webhook, envía una transacción real de prueba Sandbox. El simulador genérico de PayPal puede no servir para validar por postback la firma del webhook; sigue las limitaciones que describe la documentación oficial.[Webhooks](https://developer.paypal.com/api/rest/webhooks/rest/)

Usa `Sandbox` en todos los componentes durante las pruebas: credenciales, URL base de PayPal, cuenta vendedora y cuenta compradora. No cambies solo una variable a `Live` sin implementar y verificar ese entorno completo.

## 9. Límites específicos de la implementación actual

- `OpcionesPayPal.Validar()` del backend de Pacific Nexus Airways acepta actualmente solo `Sandbox`, aunque el cliente tenga una URL base condicional para Live. No asumas que cambiar `PayPal:Entorno` a `Live` basta para habilitar producción.
- `ClientId`, `ClientSecret`, `MerchantId`, `ReturnUrl` y `CancelUrl` son obligatorios en esta implementación. `WebhookId` se valida únicamente cuando se procesa un webhook.
- El importe se toma de la reserva e incluye el IVA ya calculado por el backend; no se vuelve a sumar IVA al crear/capturar la orden.
- La moneda está fijada a USD y la orden espera un único `purchase_unit` y un único capture.
- Los reembolsos automáticos no forman parte del flujo implementado.

## Referencias del proyecto

- [Configuración PayPal](<C:/Users/Josue/Desktop/Aplicaciones Ditribuidas/Proyecto/Vuelos_Backend/Vuelos_Backend/Models/Pagos/OpcionesPayPal.cs>)
- [Cliente REST PayPal](<C:/Users/Josue/Desktop/Aplicaciones Ditribuidas/Proyecto/Vuelos_Backend/Vuelos_Backend/Services/Pagos/PayPalClient.cs>)
- [Servicio de pagos](<C:/Users/Josue/Desktop/Aplicaciones Ditribuidas/Proyecto/Vuelos_Backend/Vuelos_Backend/Services/Pagos/PagoPayPalService.cs>)
- [Controlador](<C:/Users/Josue/Desktop/Aplicaciones Ditribuidas/Proyecto/Vuelos_Backend/Vuelos_Backend/Controllers/Pagos/PayPalController.cs>)
- [Guía PayPal existente](<C:/Users/Josue/Desktop/Aplicaciones Ditribuidas/Proyecto/Vuelos_Backend/Vuelos_Backend/PAYPAL.md>)

## Documentación oficial

- [Autenticación OAuth REST](https://developer.paypal.com/api/rest/authentication)
- [Orders v2](https://developer.paypal.com/api/orders/v2/)
- [Estados de órdenes](https://developer.paypal.com/api/orders/v2/definitions/order_status/)
- [Webhooks REST](https://developer.paypal.com/api/rest/webhooks/rest/)
- [Referencia de webhooks](https://developer.paypal.com/api/webhooks/v1/)
- [Guía de seguridad PayPal](https://developer.paypal.com/security-guidelines)