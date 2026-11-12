# Entrega al equipo frontend web: ampliaciones del backend

**Aplicación:** panel de administración del Sistema de Gestión de Entregas y Rutas.  
**Contrato:** OpenAPI 1.1.0.  
**Referencia:** respuesta a `RETROALIMENTACION_BACKEND.md`.

## 1. Qué se implementó

Se completaron estas ampliaciones conservando los endpoints y campos que el panel ya consume:

| Área | Disponible ahora | Cambio solicitado en web |
|---|---|---|
| Pedidos | Remitente, correos, observaciones, tipo de carga, peso, fragilidad y prioridad | Ampliar formulario y detalle |
| Edición | Actualización completa de pedidos pendientes con comprobación de versión | Habilitar edición solo en estados permitidos |
| Resumen | Totales calculados en servidor sobre todos los registros filtrados | Sustituir los indicadores derivados de la página cargada |
| Filtros | Búsqueda textual y rango de fechas | Enviar filtros al backend y compartirlos entre listado y resumen |
| Mapa GPS | Consulta inicial de posiciones y evento SignalR | Mostrar el repartidor con hora de captura y precisión |
| Transferencia | Movimiento atómico de un pedido entre rutas | Usar el nuevo endpoint en la confirmación del arrastre |
| Orden de paradas | Reordenamiento completo con validación del tramo iniciado | Enviar el orden completo y la versión de la ruta |
| Errores | `traceId` en los Problem Details del manejador | Conservar el identificador para reportar fallos |

La migración ya se aplicó al entorno local del backend sin borrar usuarios, pedidos ni rutas. Se verificaron 51 pruebas unitarias, 14 de integración HTTP/SignalR y una prueba adicional contra PostgreSQL aislado. Esto no sustituye la validación conjunta con el panel y un dispositivo móvil.

## 2. Acceso y documentación

- Usar la URL base del backend configurada por entorno: por ejemplo `http://IP_DEL_BACKEND:5289` en la red de desarrollo.
- Swagger: `/swagger`.
- Documento actualizado: `/openapi/v1.json`, con versión informativa `1.1.0` y 36 operaciones verificadas al entregar.
- Mantener `Authorization: Bearer <accessToken>` y comprobar el rol `Administrador`.
- El cambio obligatorio de credenciales y la expiración de sesión siguen vigentes. No hay refresh tokens.
- La web nunca necesita la clave de firma JWT ni las credenciales de PostgreSQL.

Para integrar, acompañar este documento con [OpenAPI 1.1.0](contratos/openapi-1.1.0.json). Los ejemplos ejecutables del repositorio backend están en `Api/Ampliaciones.http`.

## 3. Pedidos: formulario y detalle

### Compatibilidad del destinatario

`clienteNombre` y `clienteTelefono` siguen siendo los campos de escritura y ahora se identifican explícitamente como datos del **destinatario**. La respuesta también expone `destinatarioNombre` y `destinatarioTelefono` como alias de esos mismos valores. No enviar esos alias como sustitutos de los campos actuales.

### Nuevos datos opcionales

`POST /api/pedidos` acepta el objeto opcional `detalles`. El `PedidoDto` de lectura lo devuelve también, incluso dentro de las paradas de una ruta.

```json
{
  "clienteNombre": "Persona destinataria",
  "clienteTelefono": "0991111111",
  "direccion": "Dirección de entrega",
  "latitud": -0.18,
  "longitud": -78.48,
  "fechaEntrega": "2026-12-20",
  "horaEntrega": "14:00:00",
  "zonaId": null,
  "detalles": {
    "remitenteNombre": "Persona remitente",
    "remitenteTelefono": "0992222222",
    "remitenteCorreo": "origen@example.com",
    "destinatarioCorreo": "destino@example.com",
    "observaciones": "Entregar en recepción",
    "tipoCarga": "General",
    "pesoKg": 2.5,
    "fragil": true,
    "prioridad": "Alta"
  }
}
```

Reglas para los controles del formulario:

| Campo | Validación |
|---|---|
| Remitente | Nombre y teléfono se informan juntos; máximo 150 y 30 caracteres |
| Correos | Formato válido y máximo 254 caracteres |
| Observaciones | Máximo 2000 caracteres |
| Tipo de carga | `General`, `Documento`, `Alimentos`, `Medicamentos`, `Otro` |
| Peso | Kilogramos; mayor que 0, máximo 10000 y hasta 3 decimales |
| Frágil | `true`, `false` o `null` |
| Prioridad | `Baja`, `Normal`, `Alta`, `Urgente` |

`detalles` y sus campos pueden ser null. Mostrar «No informado» cuando corresponda. No convertir `fragil: null` en «No» ni completar datos históricos con valores inventados.

**Peso, fragilidad y prioridad son informativos:** todavía no modifican la capacidad ni la optimización de rutas. El límite operativo sigue siendo la cantidad máxima de pedidos.

Se mantiene la respuesta del registro `{ pedido, asignacion }`: `EnEspera` es un registro exitoso sin asignación y `asignacion: null` es válido para pedidos futuros.

## 4. Edición de un pedido

**Endpoint nuevo:** `PUT /api/pedidos/{id}` — administrador.

1. Consultar el pedido y conservar su `version`.
2. Habilitar edición únicamente si está `Pendiente`.
3. Enviar todos los datos del formulario anterior más `version`.
4. Reemplazar los datos locales por el `PedidoDto` devuelto.

Es un **reemplazo completo**: omitir `detalles` o enviarlo null elimina los detalles anteriores. Al editar un campo, reenviar los demás datos que deban conservarse.

- Si está `AsignadoARuta`, debe retirarse de su ruta antes de editarlo.
- No se editan pedidos `EnCamino`, `Entregado` ni `Cancelado`.
- Una versión antigua devuelve 409: recargar y pedir al usuario revisar sus cambios.
- Editar la fecha a hoy no asigna automáticamente el pedido; se utiliza posteriormente el armado o la asignación manual.
- Se conservan el identificador y la fecha de registro original.

Si cambia la fecha de entrega, el evento `PedidoActualizado` llega a los grupos de la fecha anterior y de la nueva con los datos actuales. Reaplicar los filtros o recargar para retirar el registro del panel donde ya no corresponde.

## 5. Resumen y filtros generales

**Endpoint nuevo:** `GET /api/resumen/pedidos` — administrador.

Comparte estos filtros con `GET /api/pedidos`:

| Parámetro | Comportamiento |
|---|---|
| `fecha` | Fecha exacta de entrega, `YYYY-MM-DD` |
| `desde`, `hasta` | Rango inclusivo; se admite un solo extremo |
| `zonaId` | Zona opcional |
| `estado` | Estado opcional del pedido |
| `buscar` | Hasta 150 caracteres; destinatario, teléfono, dirección o nombre del remitente |

No combinar `fecha` con `desde`/`hasta`. `desde` no puede ser posterior a `hasta`. La búsqueda textual no distingue mayúsculas/minúsculas, pero no promete ignorar diferencias de acentuación.

Ejemplo:

```http
GET /api/resumen/pedidos?desde=2026-12-01&hasta=2026-12-31
Authorization: Bearer <token>
```

Ejemplo ilustrativo de respuesta:

```json
{
  "total": 10,
  "noCancelados": 8,
  "entregados": 4,
  "porcentajeEntregado": 50,
  "porEstado": [
    { "estado": "Pendiente", "cantidad": 2 },
    { "estado": "AsignadoARuta", "cantidad": 1 },
    { "estado": "EnCamino", "cantidad": 1 },
    { "estado": "Entregado", "cantidad": 4 },
    { "estado": "Cancelado", "cantidad": 2 }
  ],
  "porZona": [
    { "zonaId": "00000000-0000-0000-0000-000000000001", "cantidad": 10 }
  ],
  "calculadoEn": "2026-12-20T15:00:00Z"
}
```

Los indicadores incluyen todos los pedidos filtrados, independientemente de `pagina` y `tamano`. El porcentaje es `entregados / noCancelados × 100`, redondeado a dos decimales; si no hay denominador devuelve 0. Un filtro por estado también restringe el denominador. No etiquetarlo como cumplimiento SLA o puntualidad.

Enviar los mismos filtros al listado y al resumen. El CSV actual del frontend sigue describiendo los registros cargados; no se agregó exportación masiva.

## 6. GPS en el mapa administrativo

### Carga inicial

```http
GET /api/seguimiento/ubicaciones?fecha=2026-12-20
Authorization: Bearer <token>
```

Se admite `zonaId` opcional. La respuesta es una lista de últimas posiciones conocidas. Para un repartidor individual: `GET /api/repartidores/{id}/ubicacion`; devuelve 404 si nunca informó posición.

### Evento nuevo

Conectar al hub `/hubs/seguimiento` con el token vigente e invocar `SuscribirseAFecha` con una fecha `YYYY-MM-DD`.

Escuchar **`UbicacionRepartidorActualizada`**. Su carga es directamente:

```json
{
  "repartidorId": "00000000-0000-0000-0000-000000000001",
  "rutaId": "00000000-0000-0000-0000-000000000002",
  "latitud": -0.18,
  "longitud": -78.48,
  "precisionMetros": 12,
  "capturadaEn": "2026-12-20T15:00:00Z",
  "recibidaEn": "2026-12-20T15:00:01Z",
  "esReciente": true
}
```

No viene envuelto en `{ actualizada, ubicacion }`; ese envoltorio pertenece a la respuesta HTTP que recibe Flutter al publicar.

### Comportamiento solicitado en el mapa

- Registrar los manejadores una sola vez; suscribirse y consultar REST para recuperar el estado inicial.
- Al reconectar, volver a suscribirse y consultar REST: SignalR no reproduce los eventos perdidos.
- Al cambiar de fecha, cancelar la suscripción anterior con `CancelarFecha` y suscribirse a la nueva.
- Mantener un marcador por `repartidorId`; no reemplazar una muestra por otra anterior o igual según `capturadaEn`.
- Reaplicar el filtro de zona: el evento incluye rutaId pero no zonaId; resolverla desde la ruta y recargar datos si todavía no se conoce.
- Mostrar hora de captura y precisión. `esReciente` usa dos minutos desde captura al consultar/emitir; recalcular la antigüedad en la UI aunque no lleguen eventos nuevos.
- Cuando falte información o se vuelva antigua, mostrar «Sin ubicación» o «Última ubicación conocida», según corresponda.
- Al terminar una ruta, la última posición permanece fechada; no inferir que el conductor sigue allí.

La información depende de que Flutter publique posiciones con permisos GPS y una ruta propia `EnCurso`. Se conserva solo la última ubicación de cada repartidor: no se entrega un historial por fecha ni una línea del recorrido realizado.

**Las líneas entre destinos siguen siendo referencias geométricas.** No se agregó ruteo por calles ni ETA.

## 7. Transferencia desde el tablero de rutas

**Endpoint nuevo:** `POST /api/rutas/{origenId}/pedidos/{pedidoId}/transferir`.

```json
{
  "destinoRutaId": "00000000-0000-0000-0000-000000000003",
  "versionOrigen": 123,
  "versionDestino": 124,
  "posicion": 0
}
```

El arrastre debe abrir la confirmación actual y, al aceptar, llamar una sola vez a este endpoint. **No combinar DELETE + POST para simular la transferencia.**

- Las rutas deben ser distintas, compartir fecha y zona y estar `Planificada` o `EnCurso`.
- El pedido debe estar `AsignadoARuta` y el destino debe tener capacidad.
- `posicion` es índice base 0; null agrega al final.
- La inserción no puede alterar el tramo ya iniciado del destino.
- La respuesta contiene `{ origen: RutaDto, destino: RutaDto }`. Actualizar ambas rutas y sus versiones.
- Ante 409, recargar ambas y solicitar una nueva confirmación.
- Si el origen queda vacío, no cambia automáticamente a completado/cancelado; puede cancelarse manualmente si está planificado.

Se emiten `RutaActualizada` para ambas rutas y `PedidoActualizado` para el pedido.

## 8. Reordenamiento de paradas

**Endpoint nuevo:** `PUT /api/rutas/{id}/paradas/orden`.

```json
{
  "pedidosEnOrden": [
    "00000000-0000-0000-0000-000000000001",
    "00000000-0000-0000-0000-000000000002"
  ],
  "version": 123
}
```

Enviar los ids de **pedidos**, no los de paradas. La lista debe contener todos los pedidos de la ruta exactamente una vez. En curso, el prefijo hasta la última parada en camino o entregada permanece fijo.

La respuesta es `RutaDto` y se emite `RutaActualizada`. El `orden` recibido empieza en 1, aunque las posiciones de inserción comiencen en 0.

## 9. Versiones, errores y reintentos

`PedidoDto` y `RutaDto` agregan `version`. Guardarla junto con el registro y reenviar la última consultada en edición, transferencia y reordenamiento. Es opaca: no incrementarla manualmente.

| Código | Acción en web |
|---|---|
| 400 | Mostrar errores de campos o formato; conservar el formulario |
| 401 | Gestionar sesión ausente, vencida o revocada |
| 403 | Informar falta de permiso o cambio obligatorio de credenciales |
| 404 | Recargar o informar que el recurso no está disponible |
| 409 | Recargar las entidades afectadas y pedir revisión/confirmación |
| 422 | Mostrar la regla de negocio incumplida |
| 429 | Respetar la limitación de intentos de autenticación/cambio de credenciales |

Ejemplo de conflicto:

```json
{
  "title": "Conflicto de concurrencia",
  "status": 409,
  "detail": "El pedido cambió. Recargue antes de editar.",
  "instance": "/api/pedidos/00000000-0000-0000-0000-000000000001",
  "traceId": "identificador-de-la-solicitud"
}
```

Los errores de FluentValidation incluyen además `errors` por campo. Conservar `traceId` en los reportes de incidencias. No confirmar cambios visuales antes del éxito HTTP ni reintentar automáticamente escrituras ante un fallo o una respuesta incierta.

Todavía no se implementan ETag/If-Match ni Idempotency-Key. Las versiones protegen las nuevas modificaciones; no garantizan deduplicación de los POST antiguos de registro y armado.

## 10. Elementos del diseño que siguen pendientes

Mantenerlos deshabilitados o identificados como no disponibles:

- Borradores de pedidos con datos incompletos.
- Vehículo, placa, fotografía y batería.
- ETA, distancia vial, geometría por calles y tráfico.
- Ventanas horarias, SLA, riesgo y retraso.
- Selección de algoritmos, parámetros de optimización y rebalanceo global.
- Mapa de demanda agregado y emisiones.
- Exportación masiva e idempotencia de las operaciones antiguas.

No completar estos elementos mediante valores simulados o inferencias del estado operativo.

## 11. Checklist para la integración web

- [ ] Actualizar modelos/servicios usando el OpenAPI 1.1.0 entregado.
- [ ] Mantener las hojas de estilo y estructura visual aprobadas.
- [ ] Integrar los detalles opcionales del pedido sin romper registros antiguos.
- [ ] Permitir edición solo de pendientes y conservar la versión consultada.
- [ ] Sustituir los contadores de la página por el resumen del servidor.
- [ ] Compartir filtros entre tabla, resumen y mapa según su alcance.
- [ ] Consultar GPS por REST y aplicar eventos, reconexión y caducidad visual.
- [ ] Usar transferencia atómica desde la confirmación del arrastre.
- [ ] Reordenar mediante la lista completa y la versión actual de la ruta.
- [ ] Manejar conflictos y errores sin doble envío ni confirmaciones anticipadas.
- [ ] Probar con un administrador real y un conductor de pruebas que publique GPS.
- [ ] Validar conjuntamente la UI y el comportamiento con datos representativos antes de liberar.

## Archivos de referencia

- [OpenAPI exportado 1.1.0](contratos/openapi-1.1.0.json).
- [Contrato detallado de las ampliaciones](Ampliaciones_Backend_Integracion.md).
- [Autenticación y roles](Autenticacion_JWT_Integracion.md).
- Ejemplos HTTP del backend: `Api/Ampliaciones.http` y `Api/Autenticacion.http`.

Al compartir fuera del repositorio backend, adjuntar también el JSON OpenAPI para que los enlaces relativos no sean la única fuente del contrato.
