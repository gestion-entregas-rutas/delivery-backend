# Guía de implementación frontend: intervalos de entrega (etapas 5–8)

Esta guía explica cómo conectar las tres aplicaciones con el backend de intervalos de entrega:

- **Tienda** (`Ecommerce`, Angular, puerto 4300): el cliente elige un intervalo.
- **Panel** (`delivery-frontend-web`, Angular, puerto 4200): el personal ve estimaciones y alertas, y valida los cambios de ruta.
- **App del repartidor** (`delivery-frontend-mobile`, Flutter): el repartidor ve el compromiso, la estimación y confirma la entrega.

El backend ya está implementado y probado. Esta guía no cambia ningún frontend: describe qué debe hacer cada uno.

---

## 0. Reglas que la interfaz debe respetar

1. **El compromiso se cumple cuando la entrega se completa dentro del intervalo aceptado.** Llegar dentro del intervalo no basta: la atención también tiene que terminar antes del límite.
2. **Mostrar un intervalo no lo reserva.** El checkout vuelve a comprobarlo. Si otra persona tomó el último cupo, responde `409 INTERVALO_NO_DISPONIBLE`.
3. **La reserva es temporal.** Mientras el pago está pendiente (15 minutos, `expiraEn`), el intervalo ocupa capacidad. Si el pago vence o se cancela, la capacidad se libera.
4. **Las estimaciones son opcionales.** Si llegan `null`, mostrar **"Sin estimación"**. Nunca mostrar una hora inventada ni usar `horaEntrega` como si fuera una estimación.
5. **El intervalo comprometido solo cambia con una reprogramación explícita.** Una estimación nueva nunca modifica `compromisoInicioEn` ni `compromisoFinEn`.
6. **La puntualidad la calcula el backend** (`puntualidad.estado` y `puntualidad.retrasoMinutos`). El frontend no la recalcula, solo la muestra.
7. **El modelo de tiempos es aproximado** (`modeloEstimacion = "HaversineAjustadoV1"`: distancia en línea recta × factor ÷ velocidad promedio + margen). Mostrar las horas como *estimadas*, por ejemplo con el texto "aprox." o "estimado".
8. **Zona horaria:** el negocio opera en `America/Guayaquil`. Los instantes llegan en ISO 8601 en UTC (`...Z`). Mostrarlos con `timeZone: 'America/Guayaquil'`. Al reenviar un intervalo, enviar **exactamente** los valores `inicioEn` y `finEn` recibidos, sin reconstruirlos.

---

## 1. Preparación del backend (antes de probar)

```powershell
cd delivery-backend
dotnet ef database update -p Infrastructure -s Infrastructure
```

La migración `IntervalosEntregaReservas` solo agrega columnas nullable en `Ordenes` (`CompromisoInicioEn`, `CompromisoFinEn`) y un índice. No modifica datos existentes.

Configuración (`Api/appsettings.json`, sección `PlanificacionRutas`; también acepta variables de entorno `PlanificacionRutas__...`):

| Clave | Valor inicial | Uso |
|---|---|---|
| `FinOfertaIntervalos` | `18:00:00` | Último fin de intervalo que se ofrece cuando no hay `FinJornada`. |
| `OrigenLatitud` / `OrigenLongitud` | `null` | Ubicación del local. Configurarla con las coordenadas reales. |
| `DiasMaximosOferta` | `30` | Cuántos días hacia adelante se pueden consultar intervalos. |
| `DuracionIntervaloMinutos` | `30` | Duración de cada intervalo. |

Si no se configura el origen, el backend usa el origen de la última ruta armada con GPS. Si no existe ninguno, la oferta responde `422` con un mensaje claro. **Recomendación:** configurar el origen del local en el servidor.

---

## 2. Contrato del API

### 2.1 Consultar intervalos (tienda y panel)

`GET /api/entregas/intervalos?fecha=2027-02-14&latitud=-1.25&longitud=-78.62`

Requiere JWT de **Cliente** o **Administrador**. Un conductor recibe `403`.

Respuesta `200`:

```json
{
  "fecha": "2027-02-14",
  "zonaId": "…",
  "zonaNombre": "Centro",
  "duracionMinutos": 30,
  "modeloEstimacion": "HaversineAjustadoV1",
  "calculadoEn": "2027-02-13T15:02:11Z",
  "intervalos": [
    { "inicioEn": "2027-02-14T13:00:00Z", "finEn": "2027-02-14T13:30:00Z", "disponible": true,  "motivo": null },
    { "inicioEn": "2027-02-14T13:30:00Z", "finEn": "2027-02-14T14:00:00Z", "disponible": false, "motivo": "SinCapacidad" }
  ]
}
```

- `motivo`: `null` si está disponible, `"Vencido"` si ya pasó o `"SinCapacidad"` si no cabe (no hay repartidor, no hay cupo o comprometería otras entregas).
- Errores `422` (ProblemDetails con `detail`): dirección sin cobertura, fecha pasada, fecha fuera de `DiasMaximosOferta` u origen del local no configurado.

### 2.2 Checkout con intervalo (tienda)

`POST /api/ordenes/checkout`. En `datosDomicilio` se agregan dos campos **opcionales**:

```json
"datosDomicilio": {
  "destinatarioNombre": "…", "destinatarioTelefono": "…", "direccionTexto": "…",
  "latitud": -1.25, "longitud": -78.62,
  "fechaEntrega": "2027-02-14",
  "horaEntrega": "08:00:00",
  "observaciones": "",
  "compromisoInicioEn": "2027-02-14T13:00:00Z",
  "compromisoFinEn": "2027-02-14T13:30:00Z"
}
```

- Los dos campos van juntos. Si solo llega uno, la respuesta es `422`.
- El intervalo tiene que ser **uno de la rejilla ofrecida** para esa fecha. Si no, la respuesta es `422`.
- `horaEntrega` sigue siendo obligatorio por compatibilidad. Con intervalo, el backend lo reemplaza por la hora local de inicio. Se puede enviar la misma hora de inicio en formato `HH:mm:ss`.
- Sin estos campos, el checkout funciona igual que antes, sin compromiso. La tienda nueva **debe** enviarlos.

Respuesta `201`: `CheckoutResponse` agrega `compromisoInicioEn` y `compromisoFinEn` (o `null`).

Error nuevo:

| HTTP | `codigo` | Significado | Acción de la UI |
|---|---|---|---|
| 409 | `INTERVALO_NO_DISPONIBLE` | Otro cliente tomó el cupo o el intervalo venció. Incluye `compromisoInicioEn` y `compromisoFinEn`. | Avisar, volver a consultar los intervalos y pedir otra elección. **No** reintentar con la misma clave de idempotencia. |

Al confirmar el pago (simulado o captura de PayPal), el pedido logístico se crea con ese compromiso. No se vuelve a comprobar la capacidad, porque ya quedó reservada.

### 2.3 Mis órdenes (tienda)

`GET /api/ordenes/mias`: cada `OrdenMiaDto` agrega:

- `compromisoInicioEn` y `compromisoFinEn`.
- `finalizacionEstimadaEn`: estimación de la parada en su ruta, o `null`.
- `puntualidad`: `{ estado, retrasoMinutos }`, o `null` mientras no exista pedido logístico.

### 2.4 Rutas (panel y Flutter)

`RutaDto` (en `GET /api/rutas`, `GET /api/rutas/{id}`, en las respuestas de los ajustes y en el evento SignalR `RutaActualizada`) agrega:

- `alertas: [{ pedidoId, orden, estado, retrasoMinutos }]`: paradas en estado `RetrasoPrevisto` o `PlazoVencido`. Se calculan en cada consulta; no se guardan.
- Campos que ya existían desde las etapas 1–4: `origen`, `salidaProgramadaEn`, `estimacionesActualizadasEn`, `modeloEstimacion`, `distanciaEstimadaKm`.
- `ParadaDto`: `llegadaEstimadaEn`, `inicioAtencionEstimadoEn`, `finalizacionEstimadaEn`, `trasladoEstimadoMinutos` (todos opcionales).
- `PedidoDto`: `compromisoInicioEn`, `compromisoFinEn`, `tiempoServicioMinutos`, `puntualidad`.

Desde ahora, **las estimaciones se recalculan** en vez de borrarse cuando:

- se agrega, quita, reordena o transfiere una parada;
- se inicia la ruta;
- el repartidor marca "en camino" o "entregado". El cálculo parte de la posición más reciente conocida: GPS, última entrega o salida.

Si la ruta no tiene origen conocido, las estimaciones siguen en `null` y se muestra "Sin estimación".

### 2.5 Validación de cambios de ruta (panel)

Los requests de ajuste aceptan un campo opcional `forzar` (por defecto `false`):

- `POST /api/rutas/{id}/pedidos` → `{ "pedidoId": "…", "posicion": 0, "forzar": false }`
- `PUT /api/rutas/{id}/paradas/orden` → `{ "pedidosEnOrden": [...], "version": 12, "forzar": false }`
- `POST /api/rutas/{id}/pedidos/{pedidoId}/transferir` → `{ "destinoRutaId": "…", "versionOrigen": 3, "versionDestino": 5, "posicion": null, "forzar": false }`

Si el cambio hace que una entrega que **antes cumplía** deje de cumplir, la respuesta es:

```json
HTTP 409
{
  "title": "Conflicto de concurrencia",
  "detail": "El cambio haría que otras entregas no se completen dentro de su intervalo comprometido. …",
  "codigo": "CAMBIO_PONE_EN_RIESGO",
  "pedidosEnRiesgo": [
    { "pedidoId": "…", "rutaId": "…", "finalizacionEstimadaEn": "…", "limiteEn": "…", "retrasoMinutos": 31 }
  ]
}
```

En ese caso **no se guarda nada**. Si el usuario confirma, se reenvía el mismo request con `"forzar": true`. El cambio se guarda y las alertas quedan visibles en `alertas`.

Las entregas que ya estaban atrasadas antes del cambio no bloquean el cambio. Quitar una parada nunca se rechaza.

Nuevo endpoint (solo Administrador): `POST /api/rutas/{id}/estimaciones/recalcular` → `RutaDto`. Devuelve `422` si la ruta no está activa, no tiene paradas restantes o no tiene un origen conocido.

### 2.6 Estados de puntualidad (texto sugerido)

| `estado` | Texto para la UI | Estilo |
|---|---|---|
| `SinCompromiso` | Sin intervalo comprometido | neutro |
| `SinEstimacion` | Sin estimación | neutro |
| `DentroDelPlazoPrevisto` | A tiempo (estimado) | ok |
| `RetrasoPrevisto` | Retraso previsto: +N min | advertencia |
| `PlazoVencido` | Plazo vencido hace N min | error |
| `EntregadoEnIntervalo` | Entregado a tiempo | ok |
| `EntregadoConRetraso` | Entregado con N min de retraso | error |
| `EntregadoAntesDelIntervalo` | Entregado antes del intervalo | info |
| `NoAplica` | No aplica (cancelado) | neutro |

En `PlazoVencido`, `retrasoMinutos` es el tiempo que pasó desde el límite, no una predicción del retraso final.

---

## 3. Tienda (`Ecommerce`)

### 3.1 Modelos (`src/app/core/models/api.models.ts`)

```ts
export interface IntervaloEntrega { inicioEn: string; finEn: string; disponible: boolean; motivo: 'Vencido' | 'SinCapacidad' | null; }
export interface IntervalosDisponibles {
  fecha: string; zonaId: string; zonaNombre: string; duracionMinutos: number;
  modeloEstimacion: string; calculadoEn: string; intervalos: IntervaloEntrega[];
}
export interface Puntualidad { estado: string; retrasoMinutos: number | null; }
// Agregar a DatosDomicilio:   compromisoInicioEn?: string | null; compromisoFinEn?: string | null;
// Agregar a CheckoutResponse: compromisoInicioEn?: string | null; compromisoFinEn?: string | null;
// Agregar a OrdenResumen (mias): compromisoInicioEn?, compromisoFinEn?, finalizacionEstimadaEn?: string | null; puntualidad?: Puntualidad | null;
```

### 3.2 Servicio nuevo (`src/app/core/services/delivery-slots.service.ts`)

```ts
@Injectable({ providedIn: 'root' })
export class DeliverySlotsService {
  constructor(private http: HttpClient) {}
  intervalos(fecha: string, latitud: number, longitud: number) {
    const params = new HttpParams().set('fecha', fecha).set('latitud', latitud).set('longitud', longitud);
    return this.http.get<IntervalosDisponibles>(`${environment.apiBaseUrl}/entregas/intervalos`, { params });
  }
}
```

### 3.3 Cambios en `features/checkout/checkout.page.ts`

1. **Reemplazar** el `<input type="time">` de "Hora de entrega" por un **selector de intervalos** (botones o chips). Conservar el campo de fecha.
2. Consultar los intervalos cuando haya **fecha + coordenadas válidas con cobertura** (después de `validateCoverage()`). Volver a consultar si cambia la fecha o el punto, con *debounce* de unos 400 ms. Cancelar la consulta anterior con `switchMap`.
3. Estados del selector:
   - Cargando: esqueleto o spinner.
   - Intervalos con `disponible=false`: mostrarlos deshabilitados. Usar "Ya pasó" para `Vencido` y "Sin cupo" para `SinCapacidad`.
   - Ninguno disponible: "No hay horarios disponibles para esta fecha. Prueba otro día."
   - `422`: mostrar `e.userMessage`, el `detail` del backend (sin cobertura, fecha fuera de rango).
4. Formato: `08:00 – 08:30` con `Intl.DateTimeFormat('es-EC', { hour: '2-digit', minute: '2-digit', timeZone: 'America/Guayaquil' })`.
5. Guardar el intervalo elegido **tal como llegó**: `{ inicioEn, finEn }`.
6. Validación previa: si es domicilio y no hay intervalo elegido, mostrar "Elige un horario de entrega."
7. Payload (línea donde hoy se arma `payload.datosDomicilio`):

```ts
payload.datosDomicilio = {
  ...this.domicilio,
  observaciones: this.domicilio.observaciones || '',
  horaEntrega: horaLocal(this.intervalo.inicioEn) + ':00',  // compatibilidad; el backend lo sobrescribe
  compromisoInicioEn: this.intervalo.inicioEn,
  compromisoFinEn: this.intervalo.finEn,
};
```

   El intervalo forma parte de `firma`, así que cambiar de intervalo ya genera otra `Idempotency-Key`. Ese comportamiento es el correcto.

8. Manejo del error nuevo, junto al de `STOCK_AGOTADO`:

```ts
} else if (e?.status === 409 && code === 'INTERVALO_NO_DISPONIBLE') {
  this.error = 'El horario que elegiste acaba de ocuparse o ya pasó. Elige otro horario disponible.';
  this.intervalo = null;
  this.recargarIntervalos();
}
```

9. **Reserva temporal:** el contador de `startExpiryTimer(res.expiraEn)` ya existe. Agregar el texto "Tu horario de entrega está reservado hasta HH:mm. Si no completas el pago, se liberará."
10. PayPal: no cambia. Si la captura llega después de `expiraEn`, el backend responde `PAGO_NO_CONFIRMABLE` (flujo actual).
11. Comprobante o éxito: mostrar "Entrega: sáb 14 feb, 08:00 – 08:30" a partir de `res.compromisoInicioEn` y `res.compromisoFinEn`.

### 3.4 Mis órdenes y detalle

- Mostrar el intervalo comprometido si existe. En órdenes antiguas mostrar "Sin intervalo comprometido". **No** derivar un intervalo de `horaEntrega`.
- Si existe `finalizacionEstimadaEn`, mostrar "Entrega estimada: aprox. 08:12". Si es `null`, "Sin estimación".
- Etiqueta de `puntualidad` según la tabla 2.6.

### 3.5 Pruebas de la tienda (Vitest/Playwright)

- Selector: deshabilita los intervalos no disponibles y no permite comprar sin intervalo.
- El payload incluye `compromisoInicioEn` y `compromisoFinEn` idénticos a los recibidos.
- `409 INTERVALO_NO_DISPONIBLE`: muestra el mensaje, limpia la selección y vuelve a consultar.
- `422` de la oferta: muestra el `detail`.
- Cambiar la fecha o el punto vuelve a consultar; las respuestas viejas no pisan a las nuevas.
- E2E con el backend real: dos navegadores eligen el último cupo; uno compra y el otro recibe el 409.

---

## 4. Panel (`delivery-frontend-web`)

### 4.1 Armado con origen real

En `operations.service.ts → buildRoutes()`, enviar el GPS del local que ya guarda `LocalLocationService`:

```ts
const local = this.local.location();               // inject(LocalLocationService)
this.api.armarRutas({
  fecha: this.fecha,
  zonaId: this.zona || undefined,
  origen: local ? local.ubicacion : undefined,     // { latitud, longitud }
  // salidaProgramadaEn: opcional, ISO con offset, dentro de la fecha y no en el pasado
});
```

Si hay pedidos con compromiso y no se envía `origen`, el backend responde `422`. En ese caso, mostrar "Configura la ubicación del local para armar rutas con horarios comprometidos."

### 4.2 Modelos (`core/models/logistica.models.ts`)

```ts
export interface AlertaPuntualidadDto { pedidoId: Guid; orden: number; estado: 'RetrasoPrevisto' | 'PlazoVencido'; retrasoMinutos: number | null; }
// RutaDto: + alertas?: AlertaPuntualidadDto[] | null;
// Requests: AgregarPedidoARutaRequest / ReordenarParadasRequest / TransferirPedidoRequest: + forzar?: boolean;
export interface PedidoEnRiesgo { pedidoId: Guid; rutaId: Guid; finalizacionEstimadaEn: string; limiteEn: string | null; retrasoMinutos: number | null; }
```

### 4.3 Estimaciones y alertas en la vista de rutas

- En cada parada mostrar: intervalo comprometido, llegada estimada, inicio de atención (si hay espera), finalización estimada y la etiqueta de puntualidad (tabla 2.6).
- Mostrar la espera cuando `inicioAtencionEstimadoEn > llegadaEstimadaEn`: "Espera ~N min".
- En la cabecera de la ruta: contador de `alertas.length` con badge, "Estimado a las HH:mm" (`estimacionesActualizadasEn`) y la etiqueta "Aproximado (Haversine)".
- Lista global de alertas: unir las `alertas` de las rutas del día. Ordenar primero `PlazoVencido` y luego por `retrasoMinutos` descendente. Cada alerta enlaza a su parada.
- Tiempo real: el evento SignalR `RutaActualizada` ya trae `alertas` y las estimaciones recalculadas. Reemplazar la ruta completa en el estado; no fusionar campo por campo.
- Botón "Recalcular estimaciones" (Administrador): `POST /api/rutas/{id}/estimaciones/recalcular`. Si responde `422`, mostrar el `detail`.

### 4.4 Ajustes con confirmación de riesgo

Aplicarlo en `route-assignment.service.ts` (agregar o transferir) y en `route-order.service.ts` (reordenar):

```ts
async ejecutarConRiesgo<T>(enviar: (forzar: boolean) => Observable<T>) {
  try { return await firstValueFrom(enviar(false)); }
  catch (e: any) {
    const p = e?.error ?? e?.problem;
    if (e?.status === 409 && p?.codigo === 'CAMBIO_PONE_EN_RIESGO') {
      const ok = await this.confirmar(p.pedidosEnRiesgo as PedidoEnRiesgo[]); // diálogo
      if (!ok) return null;                       // nada se guardó
      return await firstValueFrom(enviar(true));  // misma versión: no hubo escritura
    }
    throw e;  // 409 de versión → flujo actual (recargar)
  }
}
```

- Diálogo: "Este cambio pondría en riesgo N entregas" y una lista con cliente, límite, finalización estimada y "+N min". Botones "Cancelar" (por defecto) y "Aplicar de todos modos".
- Diferenciar los 409: `CAMBIO_PONE_EN_RIESGO` usa el diálogo. Un 409 sin `codigo` es un conflicto de versión y sigue el flujo actual de recarga.
- El reintento con `forzar: true` usa **las mismas versiones**, porque el intento rechazado no escribió nada. Si entretanto otra persona cambió la ruta, llegará el 409 de versión habitual.

### 4.5 Formulario de pedidos (opcional)

Para pedidos manuales con horario, usar `GET /api/entregas/intervalos` con las coordenadas del pedido y enviar `compromisoInicioEn` y `compromisoFinEn` en `POST /api/pedidos`. La reprogramación explícita es `PUT /api/pedidos/{id}` con ambos extremos nuevos y solo funciona con pedidos pendientes.

### 4.6 Pruebas del panel

- `armarRutas` envía `origen` desde `LocalLocationService`.
- 409 `CAMBIO_PONE_EN_RIESGO` abre el diálogo. Cancelar no envía nada; confirmar reenvía con `forzar: true`.
- Un 409 sin código sigue recargando como antes.
- Render de "Sin estimación" con campos `null` y de cada estado de puntualidad.
- `RutaActualizada` reemplaza las alertas.

---

## 5. App del repartidor (`delivery-frontend-mobile`, Flutter)

### 5.1 Modelos (`lib/models/logistica_models.dart`)

Agregar campos opcionales y parsear con tolerancia a `null` (respuestas de versiones anteriores):

```dart
class PuntualidadDto {
  final String estado; final int? retrasoMinutos;
  PuntualidadDto.fromJson(Map<String, dynamic> j)
      : estado = j['estado'] as String? ?? 'SinEstimacion',
        retrasoMinutos = (j['retrasoMinutos'] as num?)?.toInt();
}
// PedidoDto:  DateTime? compromisoInicioEn, compromisoFinEn; int? tiempoServicioMinutos; PuntualidadDto? puntualidad;
// ParadaDto:  DateTime? llegadaEstimadaEn, inicioAtencionEstimadoEn, finalizacionEstimadaEn; double? trasladoEstimadoMinutos;
// RutaDto:    DateTime? estimacionesActualizadasEn; List<AlertaPuntualidadDto> alertas (vacía si null);
DateTime? _fecha(Object? v) => v is String ? DateTime.tryParse(v)?.toLocal() : null;
```

Mostrar las horas en la zona del negocio. El dispositivo normalmente está en Ecuador; si se quiere forzarlo, usar el paquete `timezone` con `America/Guayaquil`.

### 5.2 Pantallas

- `widgets/route/stop_row.dart` y `current_stop_card.dart`:
  - "Entregar entre 08:00 y 08:30" (compromiso) o "Sin intervalo comprometido".
  - "Llegada aprox. 08:05 · Terminar antes de 08:30".
  - Chip de puntualidad (tabla 2.6), en rojo o ámbar para `RetrasoPrevisto` y `PlazoVencido`.
  - Si `inicioAtencionEstimadoEn > llegadaEstimadaEn`: "Llegarás antes: espera hasta 08:00". Ayuda a no entregar antes de tiempo.
- `route_header.dart`: número de alertas y "Estimado a las HH:mm".
- Si los campos son `null`: "Sin estimación". No usar `horaEntrega` como estimación.

### 5.3 Confirmación de entrega

Los endpoints no cambian: `POST /api/seguimiento/pedidos/{id}/en-camino` y `.../entregado`.

- Ahora el backend **recalcula** las paradas restantes en cada avance. Después de cada acción, refrescar la ruta (`GET /api/rutas/{id}` o el evento `RutaActualizada` de SignalR) para que las horas de las paradas siguientes se actualicen.
- Antes de "Entregado", si la puntualidad es `PlazoVencido`, mostrar un aviso informativo (no bloqueante): "Esta entrega se registrará con retraso."
- Después de confirmar, mostrar el resultado que devuelve `pedido.puntualidad`: `EntregadoEnIntervalo`, `EntregadoConRetraso` o `EntregadoAntesDelIntervalo`.
- El GPS que la app ya envía (`/api/ubicaciones`) se usa como posición en el siguiente recálculo, ya sea por avance o con el botón del panel. No hace falta enviar nada más.

### 5.4 Pruebas Flutter

- `fromJson` con todos los campos nuevos y con `null` o ausentes (respuesta antigua).
- Widget de parada: muestra compromiso, estimación, espera y chip.
- Después de "entregado" se refresca la ruta y cambian las horas de la siguiente parada.

---

## 6. Escenarios de prueba manual del flujo completo (etapa 8)

| # | Escenario | Resultado esperado |
|---|---|---|
| 1 | Dirección lejana con intervalo temprano | El intervalo aparece como `SinCapacidad`; los posteriores, disponibles. |
| 2 | Un solo repartidor con capacidad 1; el cliente A deja el pago pendiente | El cliente B ve todo `SinCapacidad`. Si A cancela o vence (15 min), B vuelve a ver disponibilidad. |
| 3 | Dos clientes compran el último cupo a la vez | Uno `201` y otro `409 INTERVALO_NO_DISPONIBLE`. El stock solo se descuenta para el primero. |
| 4 | Sin repartidores disponibles | Todos los intervalos `SinCapacidad`. |
| 5 | Panel reordena y deja una entrega fuera de plazo | `409 CAMBIO_PONE_EN_RIESGO`, diálogo; con confirmación se guarda y aparece la alerta `RetrasoPrevisto`. |
| 6 | El repartidor se retrasa (entrega tarde una parada) | Las paradas siguientes se recalculan; el panel ve las alertas por SignalR. |
| 7 | Pedidos y rutas antiguos (sin compromiso ni origen) | Funcionan igual; se muestra "Sin intervalo comprometido" o "Sin estimación". |
| 8 | Checkout sin intervalo (cliente antiguo) | Funciona como antes; la orden queda sin compromiso. |

---

## 7. Límites conocidos (comunicar en la UI cuando aplique)

- Tiempos **aproximados**: distancia en línea recta ajustada, sin tráfico ni calles reales.
- La oferta usa una heurística: que un intervalo aparezca como `SinCapacidad` no demuestra que sea imposible.
- Las órdenes pagadas se convierten en pedidos `Pendiente`. Se asignan al armar rutas o, si son del mismo día y se registran por el panel, por inserción automática validada con tiempos.
- El GPS por sí solo no dispara recálculos (para no recalcular en cada ping). Los recálculos ocurren al registrar avances o con el botón "Recalcular estimaciones".
