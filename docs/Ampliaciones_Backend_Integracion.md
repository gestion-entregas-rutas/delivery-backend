# Ampliaciones para web administrativa y Flutter — contrato 1.1.0

## Alcance y compatibilidad

Se implementan las cuatro etapas acordadas: datos ampliados y edición de pedidos, GPS, indicadores/filtros y transferencia/reordenamiento atómicos. Se mantiene la estructura `Capa/Tipo/Entidad`, los endpoints existentes y los nombres de sus campos. El documento continúa en `/openapi/v1.json`; su versión informativa es `1.1.0`. Swagger incorpora los nuevos contratos.

JWT y las políticas de rol continúan vigentes. Los errores del manejador y Problem Details incorporan `traceId`. Los datos históricos permanecen sin completar artificialmente sus campos nuevos.

## 1. Datos del pedido

`clienteNombre` y `clienteTelefono` se conservan y representan al **destinatario**, no al remitente. La respuesta agrega los alias `destinatarioNombre` y `destinatarioTelefono` para facilitar el etiquetado de la UI. En escritura se siguen enviando los nombres actuales `clienteNombre` y `clienteTelefono`.

El registro y la edición aceptan el objeto opcional `detalles`; la lectura devuelve el mismo conjunto de datos. Los campos son opcionales y `null` significa no informado.

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

| Campo | Regla |
|---|---|
| remitenteNombre / remitenteTelefono | Se informan juntos; hasta 150 / 30 caracteres |
| Correos | Formato de correo válido; hasta 254 caracteres |
| observaciones | Hasta 2000 caracteres |
| tipoCarga | General, Documento, Alimentos, Medicamentos, Otro |
| pesoKg | Kilogramos; mayor que cero, máximo 10000; como máximo 3 decimales |
| fragil | Booleano opcional; null no equivale a false |
| prioridad | Baja, Normal, Alta, Urgente |

Los límites de peso y los catálogos son la configuración inicial del contrato para revisión del equipo. **Peso, fragilidad y prioridad son informativos por ahora**: el planificador sigue limitando cantidad de pedidos por ruta. No se implementa capacidad por peso ni una promesa SLA al seleccionar prioridad.

### Edición

`PUT /api/pedidos/{id}` — administrador.

Enviar el cuerpo completo anterior más `version`, tomado del último `PedidoDto`. Se conserva el identificador y `registradoEn`. Solo se edita un pedido `Pendiente`: si está asignado, primero debe retirarse de su ruta mediante la operación correspondiente. No se modifican entregados, cancelados ni pedidos en camino.

Es un reemplazo completo: `detalles: null` o su omisión elimina los detalles opcionales anteriores. La edición no dispara una asignación automática, aunque se cambie la fecha a hoy. Se utiliza después el armado o la asignación manual.

Una versión desactualizada devuelve 409. Después de editar, actualizar la copia local con el DTO devuelto. Si cambió la fecha, `PedidoActualizado` se envía tanto al grupo de la fecha anterior como al de la nueva, siempre con los datos actuales: ambos paneles deben recargar su consulta o retirar el registro si ya no coincide con sus filtros.

## 2. GPS operativo

### Publicación desde Flutter

`POST /api/repartidores/me/ubicacion` — solo conductor con credenciales definitivas.

El repartidor se identifica por el JWT. No se acepta un identificador de otro conductor en el cuerpo.

```json
{
  "rutaId": "00000000-0000-0000-0000-000000000001",
  "latitud": -0.18,
  "longitud": -78.48,
  "precisionMetros": 12,
  "capturadaEn": "2026-10-24T15:30:00Z"
}
```

Reglas:

- La ruta debe pertenecer al conductor y estar `EnCurso`.
- Latitud [-90, 90], longitud [-180, 180] y precisión [0, 1000] metros; números finitos.
- Captura con offset horario explícito; enviar UTC preferentemente. No más de cinco minutos de antigüedad ni más de un minuto en el futuro.
- Una muestra con hora anterior o igual a la almacenada no la reemplaza ni genera otro evento.
- Se conserva una sola última ubicación por repartidor, no un historial de recorridos. Los choques de escritura GPS se recargan y reintentan hasta tres veces comprobando de nuevo la antigüedad.

Respuesta 200:

```json
{
  "actualizada": true,
  "ubicacion": {
    "repartidorId": "00000000-0000-0000-0000-000000000002",
    "rutaId": "00000000-0000-0000-0000-000000000001",
    "latitud": -0.18,
    "longitud": -78.48,
    "precisionMetros": 12,
    "capturadaEn": "2026-10-24T15:30:00Z",
    "recibidaEn": "2026-10-24T15:30:01Z",
    "esReciente": true
  }
}
```

`actualizada: false` indica que la muestra no era más reciente. La respuesta contiene la posición conservada. Flutter debe mantener la hora original al recuperar conexión y descartar muestras caducadas. Frecuencia recomendada inicial: una muestra cada 10–15 segundos o al desplazarse, ajustando batería y precisión. El servidor no solicita permisos del teléfono: siguen siendo responsabilidad de Flutter.

### Consulta inicial y actualización web

| Operación | Permiso |
|---|---|
| `GET /api/repartidores/{id}/ubicacion` | Administrador o conductor consultando su propia ubicación |
| `GET /api/seguimiento/ubicaciones?fecha=YYYY-MM-DD&zonaId=...` | Administrador; zona opcional |

Una ubicación individual inexistente devuelve 404. El listado devuelve `[]` si no hay posiciones para rutas de esa fecha/zona; incluye la última conocida aunque esté desactualizada. Si el repartidor ya publicó en otra ruta, no se conserva la ubicación anterior para consultas históricas.

Hub existente: `/hubs/seguimiento`. Evento nuevo: **`UbicacionRepartidorActualizada`**, con el objeto `ubicacion` anterior (sin el envoltorio `actualizada`). Se publica a `SuscribirseAFecha(fecha)` y `SuscribirseARepartidor(repartidorId)`, respetando los permisos existentes.

La web consulta REST al abrir/reconectar y luego aplica eventos. `esReciente` se evalúa en cada lectura/emisión usando dos minutos desde captura. La web debe recalcular visualmente la antigüedad con su reloj aunque no lleguen nuevos mensajes; el evento no cambia automáticamente de true a false en el navegador.

Al completar la ruta, Flutter deja de publicar; la última posición permanece como referencia fechada. El GPS no confirma entregas ni proporciona recorridos por calles.

## 3. Indicadores y filtros

`GET /api/resumen/pedidos` — administrador.

Filtros compartidos con `GET /api/pedidos`:

- `fecha`: fecha exacta de entrega; o `desde` / `hasta`, extremos inclusivos y opcionales.
- No combinar fecha exacta con rango; `desde` no puede ser posterior a `hasta`.
- `zonaId`, `estado`.
- `buscar`: hasta 150 caracteres; busca en destinatario, teléfono, dirección y nombre del remitente, sin distinguir mayúsculas/minúsculas en campos textuales. No promete equivalencia entre caracteres acentuados y sin acento.

La paginación sigue en el listado. `pagina` y `tamano` no limitan el resumen, aunque se envíen en su query.

Respuesta: `total`, `noCancelados`, `entregados`, `porcentajeEntregado`, `porEstado` (estado/cantidad), `porZona` (zonaId/cantidad), `calculadoEn`.

Los indicadores se calculan sobre **toda la población filtrada**, en una consulta agrupada, no sobre la página cargada. `noCancelados = total − cancelados`; `porcentajeEntregado = entregados / noCancelados × 100`, redondeado a dos decimales; con denominador cero devuelve cero. Si filtra por estado, también se restringe el denominador. Es progreso de entrega, no puntualidad ni cumplimiento SLA.

No se cambia el alcance del CSV del frontend: si exporta la página cargada debe indicarlo; este endpoint no es una exportación masiva.

## 4. Transferencia, reordenamiento y concurrencia

`PedidoDto` y `RutaDto` ahora exponen `version`, basado en `xmin` de PostgreSQL. Es opaco: no incrementarlo desde el cliente ni asumir que sube de uno en uno. Conservar la versión devuelta después de cada operación y recargar ante un 409.

### Transferencia atómica

`POST /api/rutas/{origenId}/pedidos/{pedidoId}/transferir` — administrador.

```json
{
  "destinoRutaId": "00000000-0000-0000-0000-000000000003",
  "versionOrigen": 123,
  "versionDestino": 124,
  "posicion": 0
}
```

- Ambas rutas deben ser distintas, de la misma fecha y zona, y estar planificadas o en curso.
- Pedido en estado `AsignadoARuta`; no se transfiere uno en camino o entregado.
- El destino debe tener capacidad y la inserción respetar el tramo iniciado.
- `posicion` usa índice base cero; null agrega al final. El `orden` de lectura comienza en uno.
- Se comprueban ambas versiones y se guardan las rutas, sus paradas y el pedido en una sola transacción de EF Core. No implementar este flujo desde web con DELETE seguido de POST.
- Respuesta: `{ "origen": RutaDto, "destino": RutaDto }`, con versiones actualizadas. Se notifican ambas rutas y el pedido.
- Si el origen queda vacío permanece planificado; el administrador puede cancelarlo. No se completa ni cancela automáticamente.

### Reordenamiento

`PUT /api/rutas/{id}/paradas/orden` — administrador.

```json
{
  "pedidosEnOrden": ["00000000-0000-0000-0000-000000000001", "00000000-0000-0000-0000-000000000002"],
  "version": 123
}
```

Enviar todos los ids de pedidos de esa ruta exactamente una vez. No son ids de parada. En una ruta en curso, todo el prefijo hasta la última parada en camino/entregada queda fijo. El resto puede reordenarse. Respuesta `RutaDto` y evento `RutaActualizada`.

Las operaciones antiguas conservan su contrato y la protección interna de concurrencia. No se añade todavía `If-Match`/ETag ni deduplicación por `Idempotency-Key`: las versiones nuevas protegen la edición y los ajustes atómicos, pero no convierten los POST de registro/armado en idempotentes. Ante respuesta incierta, consultar el resultado antes de repetir una escritura. Esta distinción evita prometer una protección que no existe.

## Errores

Se mantienen 400 (formato/validación), 401, 403, 404, 409 y 422. Un ejemplo de versión antigua:

```json
{
  "title": "Conflicto de concurrencia",
  "status": 409,
  "detail": "El pedido cambió. Recargue antes de editar.",
  "instance": "/api/pedidos/00000000-0000-0000-0000-000000000001",
  "traceId": "identificador-de-la-solicitud"
}
```

Los errores de FluentValidation conservan además `errors` por campo. Los errores de autenticación/autorización se documentan en [Autenticacion_JWT_Integracion.md](Autenticacion_JWT_Integracion.md).

## Migración y verificación conjunta

Migración aditiva: `AmpliacionPedidosYUbicaciones`. Agrega columnas opcionales a Pedidos y la tabla UbicacionesRepartidores con geometría SRID 4326, clave única por repartidor y control de versión. Conserva usuarios y pedidos existentes.

```powershell
dotnet ef database update -p Infrastructure -s Infrastructure
dotnet test delivery.slnx
```

La prueba PostgreSQL opcional requiere `DELIVERY_TEST_CONNECTION` apuntando a un servidor de pruebas donde se puedan crear bases con PostGIS. Crea una base `delivery_test_<uuid>` aislada, aplica las migraciones y la elimina al terminar; no escribe en la base original.

Ejemplos ejecutables: `Api/Ampliaciones.http`. La fecha y los identificadores de ejemplo deben reemplazarse por los del entorno. Se entrega también una [instantánea OpenAPI 1.1.0](contratos/openapi-1.1.0.json), exportada del servidor y verificada con 36 operaciones.

Antes de liberar, probar conjuntamente: registro antiguo y ampliado, edición pendiente con versión obsoleta, resumen con más registros que la página, GPS desde dispositivo físico y reconexión web, transferencia con destino lleno y reordenamiento con entregas iniciadas.

## Fuera de esta entrega

Los borradores siguen pendientes de un contrato propio que permita datos incompletos; no se simulan con pedidos operativos. Tampoco se agregan vehículos/telemetría, ETA, geometría por calles, ventanas horarias/SLA, rebalanceo global ni emisiones. Estos elementos requieren reglas y fuentes adicionales según la retroalimentación.
