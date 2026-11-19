# Pagos PayPal Sandbox

La API usa PayPal Orders v2. El navegador o Flutter nunca recibe el `ClientSecret` ni confirma un pago por sí mismo.

## Flujo

1. El cliente autenticado crea su orden mediante `POST /api/ordenes/checkout`. La API calcula el importe y reserva inventario; el método puede indicarse como `PayPal`.
2. La tienda llama `POST /api/ordenes/{ordenId}/paypal/orden`. La respuesta contiene `paypalOrderId` y `urlAprobacion`; el cliente abre esa URL.
3. El comprador inicia sesión con una cuenta Personal Sandbox y aprueba el pago. El retorno a la tienda no confirma el pago.
4. La tienda llama `POST /api/ordenes/{ordenId}/paypal/captura` autenticado. El backend captura la orden, comprueba estado, importe USD y vendedor, y solo entonces marca pagada la orden y crea el pedido logístico de domicilio.

La ruta antigua `POST /api/ordenes/{id}/pago/confirmar` rechaza órdenes iniciadas con PayPal. Para ellas se debe usar la ruta de captura.

## Credenciales locales

En PayPal Developer Dashboard crea una aplicación REST **Sandbox**, usa su cuenta Business Sandbox como vendedor y una cuenta Personal Sandbox distinta como comprador. Configura localmente en User Secrets, no en archivos versionados:

```powershell
dotnet user-secrets set "PayPal:Habilitado" "true" --project .\Api\Api.csproj
dotnet user-secrets set "PayPal:Entorno" "Sandbox" --project .\Api\Api.csproj
dotnet user-secrets set "PayPal:ClientId" "<CLIENT_ID_SANDBOX>" --project .\Api\Api.csproj
dotnet user-secrets set "PayPal:ClientSecret" "<CLIENT_SECRET_SANDBOX>" --project .\Api\Api.csproj
dotnet user-secrets set "PayPal:MerchantId" "<MERCHANT_ID_BUSINESS_SANDBOX>" --project .\Api\Api.csproj
dotnet user-secrets set "PayPal:ReturnUrl" "http://localhost:4300/pago/paypal/retorno" --project .\Api\Api.csproj
dotnet user-secrets set "PayPal:CancelUrl" "http://localhost:4300/pago/paypal/cancelado" --project .\Api\Api.csproj
```

El API host es fijo a `https://api-m.sandbox.paypal.com`; `Entorno` solo admite `Sandbox`. `ReturnUrl` y `CancelUrl` deben ser rutas implementadas por el equipo de frontend. No coloques el JWT en esos enlaces. Para uso desde otro dispositivo, reemplaza `localhost` con una URL alcanzable por ese dispositivo.

## Base de datos

La migración `AgregarUrlAprobacionPayPal` añade `PagosOrden.UrlAprobacion`. Aplícala en la base local antes de probar:

```powershell
dotnet ef database update --project .\Infrastructure\Infrastructure.csproj --startup-project .\Api\Api.csproj
```

## Alcance actual

La integración cubre obtención de OAuth, creación y captura de órdenes, idempotencia con `PayPal-Request-Id`, y conciliación del importe/moneda/vendedor en la respuesta de captura. Webhooks, reembolsos y entorno Live no están habilitados.
