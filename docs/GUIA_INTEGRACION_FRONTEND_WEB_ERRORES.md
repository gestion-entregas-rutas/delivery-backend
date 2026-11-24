# Guía de integración: frontend web y API de E-Commerce

**API desplegada:** `https://delivery-backend-ilpi.onrender.com/api`  
**Frontend local:** `http://localhost:4300`  
**OpenAPI:** `https://delivery-backend-ilpi.onrender.com/openapi/v1.json`

Este documento resume el contrato actual del backend y cómo interpretar los errores reportados por Angular. Los `400`, `403` y `422` son respuestas distintas y requieren acciones diferentes.

## 1. Resumen de errores reportados

| Error | Qué indica | Acción para frontend / backend |
|---|---|---|
| `content-all.js: ... forEach is not a function` | Error de JavaScript en un script inyectado por el navegador o una extensión; no es una ruta del backend. | Reproducir en incógnito o desactivar extensiones. Si persiste, revisar el stack trace del propio frontend. |
| `POST /api/carrito/items/{id}` responde `400` | La ruta no coincide con el contrato actual. El endpoint para agregar un producto es `POST /api/carrito/items` y el `productoId` y `cantidad` van en el JSON. | Cambiar URL y enviar el cuerpo de la sección 2. |
| `POST /api/carrito/items` bloqueado por CORS | La respuesta no incluyó `Access-Control-Allow-Origin`; revisar el origen exacto y si el preflight/solicitud llega a la API. | El origen local esperado es `http://localhost:4300`. Si el navegador está usando otro host/puerto, agregarlo a `Cors:Origenes` en Render. Revisar además logs si la respuesta subyacente es 5xx. |
| `GET /api/zonas` responde `403` | Las rutas de zonas requieren identidad/autorización bajo la política predeterminada de la API. | No llamar desde el checkout anónimo. Solicitar al equipo backend un endpoint público de consulta de cobertura o una decisión explícita para publicar solo lectura de zonas. |
| `POST /api/ordenes/checkout` responde `422` | El JSON pasó el model binding, pero una regla del negocio lo rechazó. | Mostrar el `detail` del Problem Details. Para entrega a domicilio, lo más probable es que las coordenadas no estén dentro de una zona activa; ver sección 4. |

## 2. Contrato actual del carrito

Todos los endpoints de carrito son anónimos y pueden asociar el carrito a una sesión invitada. Al llamar `GET /api/carrito`, el backend devuelve `tokenInvitado` en el JSON y también en el header `X-Cart-Token`. El frontend debe conservarlo y enviarlo en `X-Cart-Token` en las siguientes operaciones de carrito. No usar el `productoId` como segmento de la URL al agregar.

### Leer carrito

```http
GET /api/carrito
X-Cart-Token: <token invitado, si ya existe>
```

### Agregar producto

```http
POST /api/carrito/items
Content-Type: application/json
X-Cart-Token: <token invitado, si ya existe>

{
  "productoId": "GUID-del-producto",
  "cantidad": 1
}
```

### Cambiar cantidad

```http
PUT /api/carrito/items/{productoId}
Content-Type: application/json
X-Cart-Token: <token invitado>

{ "cantidad": 2 }
```

### Quitar producto

```http
DELETE /api/carrito/items/{productoId}
X-Cart-Token: <token invitado>
```

La cantidad debe ser mayor que cero al agregar. Al actualizar, una cantidad menor o igual a cero elimina el artículo. La respuesta de carrito contiene `items`, importes y advertencias; no asumir que el cuerpo es directamente un arreglo. Esto también puede explicar un `forEach is not a function` si el frontend invoca `forEach` sobre el objeto completo en lugar de `response.items`.

Ejemplo de forma de respuesta:

```json
{
  "id": "GUID-del-carrito",
  "tokenInvitado": "token-opaco",
  "items": [
    {
      "productoId": "GUID-del-producto",
      "nombre": "Producto",
      "cantidad": 1,
      "stockDisponible": 5,
      "activo": true,
      "disponible": true,
      "subtotal": 14.99,
      "advertencia": null
    }
  ],
  "subtotal": 14.99,
  "descuento": 0,
  "envio": 0,
  "total": 14.99,
  "advertencias": []
}
```

### CORS y solicitudes con token de carrito

`X-Cart-Token` es un header personalizado. Si se usa desde el navegador, puede provocar una solicitud CORS preflight `OPTIONS`. La API permite headers arbitrarios en su política CORS, pero Render debe tener el origen exacto del navegador, incluyendo esquema y puerto:

```text
http://localhost:4300
```

No agregar una barra final al origen. Si el frontend se sirve desde otro dominio/puerto en producción, agregar ese origen en Render mediante `Cors__Origenes__N` y desplegar el cambio.

## 3. Contrato actual de checkout

`POST /api/ordenes/checkout` requiere un JWT válido de un usuario con rol `Cliente`. Enviar `Authorization: Bearer <accessToken>` y `Content-Type: application/json`. Cada elemento debe incluir `productoId` **y** `cantidad` positiva. Solo enviar los datos del método de entrega seleccionado.

### Ejemplo: retiro en tienda con PayPal

```json
{
  "items": [
    { "productoId": "GUID-producto-1", "cantidad": 1 },
    { "productoId": "GUID-producto-2", "cantidad": 2 }
  ],
  "metodoPago": {
    "tipo": "PayPal",
    "titular": null,
    "ultimos4": null,
    "referenciaSimulada": null
  },
  "tipoEntrega": "RetiroTienda",
  "datosRetiroTienda": {
    "cedulaQuienRetira": "0102030405",
    "nombreQuienRetira": "Nombre del comprador",
    "telefonoContacto": "0991234567",
    "fechaRetiro": "2026-11-20",
    "franjaHoraria": "10:00 - 12:00"
  },
  "datosDomicilio": null
}
```

La fecha debe enviarse como `YYYY-MM-DD`, compatible con `DateOnly`. No mandar cadenas vacías para los campos requeridos de retiro. Si el usuario elige domicilio, `datosRetiroTienda` debe ser `null`.

### Ejemplo: entrega a domicilio

```json
{
  "items": [
    { "productoId": "GUID-producto-1", "cantidad": 1 }
  ],
  "metodoPago": {
    "tipo": "PayPal",
    "titular": null,
    "ultimos4": null,
    "referenciaSimulada": null
  },
  "tipoEntrega": "Domicilio",
  "datosRetiroTienda": null,
  "datosDomicilio": {
    "destinatarioNombre": "Nombre del destinatario",
    "destinatarioTelefono": "0991234567",
    "direccionTexto": "Calle y referencia",
    "latitud": -0.1807,
    "longitud": -78.4678,
    "fechaEntrega": "2026-11-20",
    "horaEntrega": "14:00:00",
    "observaciones": null
  }
}
```

`latitud` y `longitud` deben ser las coordenadas reales seleccionadas por el comprador. No enviar el centro aproximado de la ciudad ni coordenadas de texto/dirección sin geocodificar. La API valida localmente que el punto esté dentro de una zona activa.

### Flujo PayPal después del checkout

1. Checkout devuelve `201` y un `ordenId`; la orden y el pago quedan pendientes mientras el comprador aprueba el pago.
2. Llamar `POST /api/ordenes/{ordenId}/paypal/orden` con el mismo JWT de cliente.
3. Redirigir al comprador a `urlAprobacion` devuelta por el backend.
4. Al volver de PayPal, llamar `POST /api/ordenes/{ordenId}/paypal/captura` con el JWT. La captura debe ejecutarla el backend; el frontend no debe marcar el pedido como pagado basándose solo en el retorno del navegador.

## 4. Interpretación de errores de checkout

La API devuelve errores de negocio en formato Problem Details. Inspeccionar `status`, `title`, `detail`, `errors` y `traceId` en Network; no tratar todos los errores como un mensaje genérico.

| HTTP | Significado en este flujo | Respuesta del frontend |
|---|---|---|
| `400` | JSON/campo inválido o conversión fallida; por ejemplo, cantidad ausente, fecha/hora no válida o formato incorrecto. | Validar formulario y mostrar errores por campo. |
| `401` | Falta JWT, expiró o sesión no válida. | Volver al login/renovar sesión. |
| `403` | El token no tiene rol `Cliente`, o la ruta consultada está restringida. | Mostrar falta de permisos; no reintentar automáticamente. |
| `404` | Producto/orden no existe. | Actualizar catálogo/carrito y pedir al usuario revisar. |
| `409` | Conflicto, como stock insuficiente; el detalle puede incluir `codigo: STOCK_AGOTADO`. | Actualizar stock/cantidad en carrito y pedir confirmación. |
| `422` | Regla de negocio incumplida. Para domicilio, posible falta de cobertura por zona activa; también fecha inválida u otras reglas. | Mostrar `detail`; conservar los datos del formulario para corregirlos. |
| `500` | Error no controlado del servidor, conexión/esquema de base u otro fallo interno. | Mostrar error temporal y compartir `traceId` con backend; no afirmar que es CORS sin revisar Network/logs. |

Un `422` no demuestra por sí mismo que el error sea cobertura. Revisar el `detail` exacto de esa respuesta.

## 5. Endpoint de zonas y cobertura

Actualmente `GET /api/zonas` es alcanzado por la política de autorización predeterminada y requiere una sesión completa; por eso una llamada desde checkout público puede responder `403`. El checkout en servidor sí verifica si las coordenadas pertenecen a una zona activa y rechaza el pedido sin cobertura.

Para que el cliente pueda validar/mostrar cobertura antes de pagar, acordar con backend una de estas opciones:

- Agregar un endpoint público de consulta de cobertura que reciba coordenadas y devuelva solo `conCobertura` (y, si se decide, información de entrega no sensible). Es la opción recomendada para no exponer polígonos internos.
- Hacer público el `GET /api/zonas` si el negocio acepta exponer los polígonos; mantener protegidos `POST`, `PUT` y operaciones administrativas.

Hasta que se publique esa capacidad, no depender de `GET /api/zonas` desde el checkout del cliente. El backend sigue siendo la autoridad final y puede responder `422` aun cuando el frontend haya hecho validaciones aproximadas.

## 6. Lectura de consola y depuración

1. En DevTools → **Network**, abrir la solicitud fallida y revisar URL/método, **Request Payload**, headers y **Response**.
2. Para CORS, comprobar el `Origin` de la página y la respuesta a `OPTIONS`; confirmar que el origen sea exactamente `http://localhost:4300`.
3. Para `400`/`422`, leer el JSON de respuesta; puede contener `errors` o `detail`.
4. Para `403`, revisar el endpoint y el JWT/rol. En este caso concreto, `/api/zonas` no es lectura anónima.
5. Para `500`, compartir con backend el `traceId` completo y hora aproximada para buscar la excepción en Render.
6. Reproducir `content-all.js` en ventana incógnita o sin extensiones antes de atribuirlo a Angular o a esta API.

## 7. Pendientes de coordinación

- **Frontend web:** corregir URL/cuerpo de agregar al carrito; persistir y reenviar `X-Cart-Token`; leer el carrito desde `response.items`; incluir cantidades y datos requeridos de checkout; usar el `detail` de errores para mensajes concretos.
- **Backend:** decidir y documentar una consulta pública de cobertura para el checkout; comprobar en Render que CORS incluya cada origen real de frontend; revisar logs con los `traceId` de errores `500`.

La especificación vigente completa está en Swagger/OpenAPI del despliegue. Si un ejemplo de este documento difiere de OpenAPI, confirmar el contrato con backend antes de codificar una suposición.
