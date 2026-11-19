# Despliegue de Delivery API en Render con Docker

Esta configuración crea un Web Service de Render desde el `Dockerfile` del repositorio. La API escucha en el puerto `PORT` que entrega Render; el valor predeterminado del contenedor local es `10000`. Render termina HTTPS en su proxy y reenvía HTTP al contenedor.

## Archivos incluidos

- `Dockerfile`: publicación multi-stage con .NET 10 y runtime ASP.NET reducido.
- `.dockerignore`: excluye pruebas, documentación, archivos locales y secretos del contexto Docker.
- `render.yaml`: define el Web Service, el health check y las variables requeridas. Los secretos usan `sync: false` para ingresarlos en el Dashboard.
- `GET /health`: endpoint anónimo que responde para el health check de Render.

## Antes de desplegar

1. Sube estos cambios al repositorio Git conectado a Render.
2. En Render, crea un **Blueprint** y selecciona este repositorio; Render leerá `render.yaml` de la raíz.
3. Cuando solicite las variables `sync: false`, ingresa los valores de producción/Sandbox directamente en Render. No los pongas en `render.yaml`, `Dockerfile`, `appsettings.json` ni en el repositorio.
4. El Blueprint trae `http://localhost:4200` y `http://localhost:4300` para pruebas locales. Para usar frontends desplegados, cambia los valores por sus orígenes HTTPS reales en Render. Escribe solo el origen, sin ruta ni barra final.
5. Configura las rutas de retorno y cancelación del frontend de producción en `PayPal__ReturnUrl` y `PayPal__CancelUrl`. En `render.yaml` están declaradas para `https://ecommerce-f2vm.vercel.app/pago/paypal/retorno` y `/pago/paypal/cancelado`; confirma que esas rutas existan en el frontend. En este archivo se mantiene PayPal en Sandbox.
6. Revisa los secretos del servicio en **Environment** antes de iniciar el deploy.

`render.yaml` configura `OpenApi__Enabled=true`, por lo que Swagger y el documento OpenAPI estarán publicados en Render para facilitar la integración. Si no quieres exponer Swagger públicamente después de la integración, cambia esa variable a `false` desde el Dashboard.

### Variables necesarias en Render

| Variable | Valor o procedencia |
|---|---|
| `ConnectionStrings__DefaultConnection` | Cadena PostgreSQL de Supabase, ingresada como secreto. Incluye SSL; no se almacena en Git. |
| `Jwt__Key` | Render genera un secreto aleatorio de 256 bits. Consérvalo estable entre despliegues para no invalidar JWT emitidos. |
| `Jwt__Issuer` | `Delivery.Api`. |
| `Jwt__Audience` | `Delivery.Clientes`. |
| `Cors__Origenes__0` | Origen permitido para el panel Angular (local por defecto: `http://localhost:4200`). |
| `Cors__Origenes__1` | Origen permitido para la tienda Angular (local por defecto: `http://localhost:4300`). |
| `PayPal__Habilitado` | `true`. |
| `PayPal__Entorno` | `Sandbox`. |
| `PayPal__ClientId`, `PayPal__ClientSecret`, `PayPal__MerchantId` | Credenciales de la aplicación y vendedor Sandbox. El secreto solo se configura en Render. |
| `PayPal__ReturnUrl`, `PayPal__CancelUrl` | Rutas HTTPS implementadas por el frontend para volver/cancelar el checkout. |

Render proporciona `PORT`; no se debe sobrescribir manualmente. La API se enlaza a `0.0.0.0:PORT` y `/health` queda disponible para la comprobación del servicio.

## Conexión a Supabase

Pega la cadena en Render como valor de `ConnectionStrings__DefaultConnection`. Usa el host/puerto del pooler indicado por Supabase y SSL. Evita incluir `Trust Server Certificate=true` si la verificación normal del certificado funciona; no guardes credenciales en archivos del repositorio.

Si una credencial real se compartió en un chat, repositorio o log, rótala en Supabase antes de publicar el servicio y actualiza el secreto de Render.

## Migraciones de base de datos

El contenedor **no ejecuta migraciones automáticamente** al iniciar. Esto evita que cada instancia o despliegue intente cambiar el esquema durante el arranque. Antes de dirigir tráfico a una base nueva, aplica las migraciones una vez desde un entorno controlado con una cadena segura:

```powershell
$env:ConnectionStrings__DefaultConnection = Read-Host "Cadena PostgreSQL de destino"
dotnet ef database update --project .\Infrastructure\Infrastructure.csproj --startup-project .\Api\Api.csproj
Remove-Item Env:ConnectionStrings__DefaultConnection
```

Comprueba las migraciones antes de habilitar tráfico. Si la base ya está actualizada, no es necesario volver a aplicarlas.

## Prueba local del contenedor

Desde la raíz del repositorio:

```powershell
docker build -t delivery-api .
docker run --rm -p 10000:10000 `
  -e PORT=10000 `
  -e ASPNETCORE_ENVIRONMENT=Production `
  -e OpenApi__Enabled=true `
  -e ConnectionStrings__DefaultConnection="<CADENA_POSTGRESQL>" `
  -e Jwt__Key="<CLAVE_JWT_DE_32_BYTES_O_MAS>" `
  delivery-api
```

Prueba `http://localhost:10000/health` y, con `OpenApi__Enabled=true`, `http://localhost:10000/swagger`.

## Verificación después del deploy

1. En Render, espera el estado **Live** y revisa los logs de inicio.
2. Abre `https://<servicio>.onrender.com/health`; debe responder HTTP 200.
3. Abre `/swagger` si `OpenApi__Enabled=true`.
4. Prueba login, catálogo y un flujo de compra de Sandbox. Revisa las URLs CORS desde los dominios desplegados.
5. Revisa los logs si PostgreSQL rechaza la conexión. No copies secretos ni JWT al compartirlos.

## Consideraciones

- `Api/appsettings.json` contiene valores locales/de ejemplo; las variables de entorno de Render los sobreescriben.
- El Dockerfile publica únicamente `Api`; `Application`, `Domain` e `Infrastructure` se compilan como referencias del proyecto.
- Webhooks PayPal, Live y administración de secretos externos no se habilitan con este despliegue.
