# Integración frontend: detalle de productos de una orden

## Endpoint agregado

```http
GET /api/ordenes/{ordenId}/detalle
Authorization: Bearer {accessToken}
Accept: application/json
```

Ejemplo de URL desplegada:

```text
https://delivery-backend-ilpi.onrender.com/api/ordenes/01a1f1fe-cd75-7e93-a36c-9db94c8dcbe2/detalle
```

Requiere una sesión válida con rol `Cliente`. La API obtiene el cliente desde el JWT y solo devuelve órdenes que pertenecen a ese cliente. Si la orden no existe o pertenece a otra persona, responde `404`.

## Respuesta `200 OK`

```json
{
  "ordenId": "01a1f1fe-cd75-7e93-a36c-9db94c8dcbe2",
  "numeroOrden": "ORD-20261119042525-266",
  "estadoOrden": "Pagada",
  "estadoPago": "Confirmado",
  "total": 44.97,
  "tipoEntrega": "RetiroTienda",
  "creadaEn": "2026-11-19T04:25:25.8997827+00:00",
  "codigoRetiro": "RET-222188",
  "fechaRetiro": "2026-11-20",
  "franjaHoraria": "10:00 - 12:00",
  "direccionEntrega": null,
  "fechaEntrega": null,
  "horaEntrega": null,
  "items": [
    {
      "productoId": "01a1d8a6-b5c8-7d70-831d-714c46a5d27b",
      "nombreProducto": "Vela perfumada",
      "precioUnitario": 14.99,
      "cantidad": 3,
      "subtotal": 44.97
    }
  ]
}
```

Los nombres y precios provienen de la instantánea guardada al crear la orden; por eso el detalle sigue siendo legible aunque el producto cambie posteriormente en el catálogo. `items` puede contener varios productos y el frontend debe iterar sobre `response.items`, no sobre la respuesta completa.

Los campos de entrega que no correspondan al tipo elegido se devuelven como `null`:

- `RetiroTienda`: `codigoRetiro`, `fechaRetiro` y `franjaHoraria` pueden tener valor.
- `Domicilio`: `direccionEntrega`, `fechaEntrega` y `horaEntrega` pueden tener valor.

## Uso desde la pantalla de pedidos

1. Consultar `GET /api/ordenes/mias` para pintar el listado y obtener `ordenId`.
2. Al abrir una orden, llamar `GET /api/ordenes/{ordenId}/detalle` con el mismo access token.
3. Mostrar `items` con nombre, cantidad, precio unitario y subtotal; mostrar el resumen de entrega y estados desde la misma respuesta.
4. Si la petición responde `401`, renovar/iniciar sesión. Si responde `404`, la orden no existe o no pertenece al usuario autenticado.

Ejemplo Angular con `HttpClient`:

```typescript
obtenerDetalleOrden(ordenId: string) {
  return this.http.get<OrdenDetalleMia>(`${this.apiUrl}/ordenes/${ordenId}/detalle`);
}
```

```typescript
export interface OrdenDetalleMia {
  ordenId: string;
  numeroOrden: string;
  estadoOrden: string;
  estadoPago: string;
  total: number;
  tipoEntrega: 'RetiroTienda' | 'Domicilio';
  creadaEn: string;
  codigoRetiro: string | null;
  fechaRetiro: string | null;
  franjaHoraria: string | null;
  direccionEntrega: string | null;
  fechaEntrega: string | null;
  horaEntrega: string | null;
  items: DetalleOrdenMia[];
}

export interface DetalleOrdenMia {
  productoId: string;
  nombreProducto: string;
  precioUnitario: number;
  cantidad: number;
  subtotal: number;
}
```

`apiUrl` debe ser `https://delivery-backend-ilpi.onrender.com/api` en el entorno publicado y apuntar al host de desarrollo correspondiente en local. El método interceptor de JWT debe adjuntar `Authorization: Bearer ...`.

## Estado de despliegue

El endpoint ya está implementado en el código del backend. Para que esté disponible en Render, se deben subir estos cambios a la rama configurada para el servicio y esperar a que termine el despliegue. Luego verificar la ruta en Swagger y consumirla desde el frontend.
