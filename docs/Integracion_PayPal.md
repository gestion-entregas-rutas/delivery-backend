# Guía de integración PayPal para Frontend E-Commerce

**Audiencia:** equipo Angular de la tienda  
**Entorno disponible:** PayPal Sandbox  
**Backend:** ASP.NET Core, rutas `/api/ordenes`  
**Autenticación:** JWT de usuario con rol `Cliente`

## Objetivo

Conectar el checkout de la tienda con PayPal Sandbox. El backend calcula el total, reserva el inventario, crea la orden PayPal y captura el pago después de que el comprador lo aprueba. La tienda no debe marcar una orden como pagada solo porque el comprador volvió desde PayPal.

## Flujo de integración

1. El cliente inicia sesión y conserva su carrito.
2. La tienda envía `POST /api/ordenes/checkout` con los productos y datos de entrega. El backend valida los productos, calcula el total y crea una orden pendiente.
3. La tienda guarda temporalmente `ordenId` y envía `POST /api/ordenes/{ordenId}/paypal/orden`.
4. El backend devuelve `urlAprobacion`. La tienda redirige el navegador a ese enlace.
5. El comprador inicia sesión con una cuenta **Personal Sandbox** y aprueba el pago.
6. PayPal redirige al `ReturnUrl` configurado. La ruta Angular de retorno llama `POST /api/ordenes/{ordenId}/paypal/captura` con el JWT del cliente.
7. La tienda muestra éxito únicamente si la respuesta del backend contiene `estadoPago: "Confirmado"`.

El endpoint de captura consulta la orden en PayPal, valida que corresponda a la orden del cliente y revisa el vendedor, el importe y la moneda antes de confirmar el pago.

## Endpoints

Todos estos endpoints requieren `Authorization: Bearer <JWT>` de un usuario con rol `Cliente`. Se recomienda configurar un interceptor Angular para agregar el token; nunca se debe agregar a las URL de retorno.

### 1. Crear checkout

`POST /api/ordenes/checkout` — responde `201 Created`.

Ejemplo para retiro en tienda:

```json
{
  "items": [
    {
      "productoId": "<UUID_PRODUCTO>",
      "cantidad": 1
    }
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

Para entrega a domicilio, usar `"tipoEntrega": "Domicilio"`, enviar `datosDomicilio` completo y poner `datosRetiroTienda` en `null`. La dirección debe estar dentro de una zona activa.

La respuesta incluye `ordenId`, `total`, `estadoPago: "Pendiente"`, `pagoId` y `expiraEn`. El inventario se reserva durante 15 minutos. No calculen ni envíen el importe como autoridad: el backend obtiene los precios del catálogo.

Se puede enviar el header `Idempotency-Key` con un UUID por intento de checkout para evitar órdenes duplicadas si el usuario repite la solicitud por un problema de red.

### 2. Crear la orden de PayPal

`POST /api/ordenes/{ordenId}/paypal/orden` — sin body.

Respuesta de ejemplo:

```json
{
  "ordenId": "<UUID_ORDEN_LOCAL>",
  "payPalOrderId": "3EU95177WJ8985248",
  "urlAprobacion": "https://www.sandbox.paypal.com/checkoutnow?token=...",
  "estadoPago": "Pendiente"
}
```

En JavaScript/TypeScript la propiedad serializada es **`payPalOrderId`** (P y P mayúsculas), tal como aparece en Swagger. Para redirigir, usar `urlAprobacion`:

```ts
window.location.assign(respuesta.urlAprobacion);
```

Guardar `ordenId` antes de salir de la tienda, por ejemplo en `sessionStorage`. No guardar el JWT junto con él ni enviarlo a PayPal.

### 3. Retorno desde PayPal y captura

Configurar una ruta Angular para el `ReturnUrl`, por defecto:

```text
http://localhost:4300/pago/paypal/retorno
```

Al cargar esa pantalla:

1. Leer el `ordenId` que la tienda guardó antes de redirigir.
2. Si no existe, no asumir que el pago fue exitoso; llevar al cliente a sus órdenes para consultar el estado.
3. Llamar `POST /api/ordenes/{ordenId}/paypal/captura` sin body y con el JWT vigente mediante el interceptor.
4. Mostrar éxito solo cuando `estadoPago` sea `Confirmado`.
5. Eliminar el `ordenId` temporal al completar el flujo.

Ejemplo conceptual:

```ts
const ordenId = sessionStorage.getItem('paypal:ordenId');
if (!ordenId) {
  // Consultar las órdenes del cliente o mostrar que no se pudo verificar el pago.
  return;
}

this.http.post<CheckoutResponse>(
  `${apiUrl}/api/ordenes/${ordenId}/paypal/captura`,
  {}
).subscribe({
  next: respuesta => {
    if (respuesta.estadoPago === 'Confirmado') {
      sessionStorage.removeItem('paypal:ordenId');
      // Mostrar confirmación y navegar al detalle de la orden.
    } else {
      // Mostrar estado pendiente; no indicar pago exitoso.
    }
  },
  error: () => {
    // Permitir reintentar la captura con el mismo ordenId.
  }
});
```

La URL de retorno de PayPal puede incluir parámetros propios de PayPal, como `token`. No usar ese valor para confirmar el pago ni reemplazar con él el `ordenId` local. La confirmación se hace siempre llamando al backend.

### 4. Consultar compras

`GET /api/ordenes/mias` — devuelve únicamente las órdenes del usuario autenticado. Úsenlo al entrar a la sección “Mis compras” y para actualizar el estado si el cliente cerró la pestaña antes de terminar el retorno.

## Ruta de cancelación

El `CancelUrl` local predeterminado es:

```text
http://localhost:4300/pago/paypal/cancelado
```

La ruta debe informar que no se confirmó el pago y ofrecer volver a la tienda. No debe llamar al endpoint de captura. Las órdenes pendientes expiran y liberan la reserva de inventario según el plazo del backend.

Por ahora no llamen automáticamente a `POST /api/ordenes/{id}/pago/cancelar` para una orden PayPal: esa ruta cancela la orden local, pero no anula la orden en PayPal. El backend debe implementar la anulación del lado de PayPal antes de usarla para este caso.

## Configuración de URLs de retorno

Los valores deben apuntar a rutas reales del frontend y coincidir con el puerto/dominio usado durante la prueba. El backend local está configurado inicialmente con `localhost:4300`; si el frontend corre en otro puerto, el equipo backend debe actualizar `PayPal:ReturnUrl` y `PayPal:CancelUrl` en User Secrets y reiniciar la API.

En Sandbox se usan una cuenta Business Sandbox como vendedor y una cuenta Personal Sandbox como comprador. La tienda solo recibe `ClientId` si la futura integración lo necesita; **nunca necesita ni debe recibir `ClientSecret`**.

## Estados que debe mostrar la tienda

| `estadoPago` | Acción en la interfaz |
|---|---|
| `Pendiente` | No mostrar éxito; permitir continuar o consultar el estado. |
| `Confirmado` | Mostrar pago exitoso y número de orden. |
| `Cancelado`, `Fallido` o `Expirado` | Informar que el pago no se completó y permitir volver al catálogo/checkout. |

Para `RetiroTienda`, la orden confirmada estará lista para retiro y tendrá `codigoRetiro`. Para `Domicilio`, el backend crea el pedido logístico al confirmar el pago y devuelve `pedidoLogisticoId` y `estadoSeguimiento`.

## Errores frecuentes

- **`400`**: revisar que el JSON esté completo y que las fechas usen formato `YYYY-MM-DD`.
- **`401`**: iniciar sesión nuevamente; el JWT puede haber expirado.
- **`403`**: confirmar que el token tenga rol `Cliente`.
- **`409`**: la orden expiró, no está aprobada o no se puede capturar en su estado actual.
- **`422`**: revisar datos del checkout, disponibilidad de producto o cobertura de la zona para domicilio.
- **Error de red durante la captura**: reintentar con el mismo `ordenId`; no crear otro checkout ni mostrar éxito hasta recibir confirmación.

## Prueba de aceptación en Swagger

1. Crear una orden con `POST /api/ordenes/checkout`.
2. Crear la orden PayPal con `POST /api/ordenes/{ordenId}/paypal/orden`.
3. Abrir `urlAprobacion`, aprobar con el comprador Sandbox y regresar a la tienda.
4. Capturar con `POST /api/ordenes/{ordenId}/paypal/captura`.
5. Confirmar `estadoPago: "Confirmado"` y verificar la orden en `GET /api/ordenes/mias`.

No se implementan webhooks, reembolsos ni pagos Live en esta entrega.
