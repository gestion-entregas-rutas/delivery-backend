# Rediseño de rutas: etapas 1–4

## Alcance implementado

Planificación temporal opcional en `POST /api/rutas/armar`, sin cambiar los campos obligatorios anteriores. Se activa al enviar `origen` con las coordenadas del local. El panel actual conserva ese GPS en `localStorage` (`LocalLocationService`); todavía no lo envía al backend. La conexión del formulario y la oferta de intervalos al cliente corresponden a etapas posteriores.

El compromiso es **completar** la entrega dentro del intervalo aceptado. La llegada, la espera y la atención son tiempos diferentes. El algoritmo no cambia compromisos para hacer que una ruta parezca puntual. No garantiza puntualidad real ni optimalidad global.

## Reglas y configuración

Sección `PlanificacionRutas` de `Api/appsettings.json` (admite variables de entorno con prefijo `PlanificacionRutas__`):

| Parámetro | Valor inicial | Significado |
|---|---|---|
| ZonaHoraria | America/Guayaquil | Interpretación de fechas y jornada; persistencia de instantes en UTC |
| InicioJornada | 08:00 | Hora indicada por el responsable |
| FinJornada | null | Todavía no acordado; no se inventa un cierre |
| DuracionIntervaloMinutos | 30 | Parámetro reservado para oferta de intervalos de etapa 5; no transforma datos históricos |
| TiempoServicioMinutos | 5 | Atención por entrega si el pedido no indica otra duración |
| MargenTrasladoMinutos | 5 | Margen adicional por tramo |
| VelocidadPromedioKmh | 20 | Supuesto inicial de prueba, no velocidad observada |
| FactorDistancia | 1.3 | Ajuste aproximado de distancia geográfica, no longitud vial real |

El origen se recibe por solicitud y se conserva en cada ruta; no se inventan coordenadas ni se usa el promedio de destinos. Para hoy, la salida predeterminada es el máximo entre ahora y apertura. Para fechas futuras es la apertura. Una salida explícita debe estar dentro de la fecha/jornada y no en el pasado. No se admiten jornadas que crucen medianoche en esta versión.

## Cálculo y asignación

1. Separar pedidos pendientes por fecha y zona, manteniendo el modelo existente.
2. Considerar todos los repartidores disponibles y su capacidad efectiva; no limitar su número solo por capacidad.
3. Procesar compromisos por vencimiento, con desempates estables. `HoraEntrega` se mantiene como referencia secundaria para pedidos anteriores, sin tratarla como plazo aceptado.
4. Probar todas las posiciones de inserción en rutas abiertas. Recalcular la ruta completa para cada candidata.
5. Traslado = Haversine × factor / velocidad × 60 + margen. Inicio de atención = máximo entre llegada y comienzo del compromiso. Finalización = inicio de atención + servicio.
6. Descartar candidatas que excedan un compromiso o el cierre configurado. Entre las viables, preferir menor distancia adicional. Si ninguna ruta abierta es viable, probar otros repartidores.
7. Dejar pendiente lo que no se pueda asignar; `pedidosSinAsignar` cuenta los pendientes. Que esta heurística no encuentre una asignación no demuestra que no exista alguna solución global.

Se mantienen la concurrencia optimista y los índices existentes para evitar doble asignación. La selección entre zonas sigue el orden anterior y no optimiza globalmente la flota entre zonas.

## Persistencia y compatibilidad

- `Pedidos`: `CompromisoInicioEn`, `CompromisoFinEn`, `TiempoServicioMinutos` opcionales. Ambos extremos deben enviarse juntos, en orden y en la fecha local del pedido. El tiempo de atención individual, si se especifica, debe caber en el intervalo.
- `Rutas`: coordenadas de origen, salida programada, fecha de cálculo, modelo y distancia estimada opcionales.
- `ParadasRuta`: llegada, inicio de atención, finalización y minutos de traslado estimados opcionales.
- Sin backfill de compromisos o estimaciones históricos. `HoraEntrega` permanece intacta. Al editar con el contrato antiguo se conserva un compromiso existente; para reprogramarlo se envían ambos extremos nuevos. Cambiar de fecha sin reprogramarlo se rechaza.
- `EntregadoEn` sigue siendo la finalización real. `puntualidad` se calcula en los DTO y no se persiste: SinCompromiso, SinEstimacion, DentroDelPlazoPrevisto, RetrasoPrevisto, PlazoVencido, EntregadoEnIntervalo, EntregadoConRetraso, EntregadoAntesDelIntervalo o NoAplica (cancelado).
- `retrasoMinutos` redondea hacia arriba minutos positivos. En PlazoVencido representa tiempo transcurrido desde el límite, no predicción del retraso final. En consultas de pedido sin ruta no se presenta una estimación de ruta: puede aparecer SinEstimacion hasta consultar la ruta.

## Uso del API

Ejemplo de creación administrativa de pedido (zona inferida por coordenadas; utilizar coordenadas reales cubiertas por una zona):

```json
{
  "clienteNombre": "Cliente de prueba",
  "clienteTelefono": "0991234567",
  "direccion": "Dirección de prueba",
  "latitud": -1.25,
  "longitud": -78.62,
  "fechaEntrega": "2027-02-14",
  "horaEntrega": "10:00:00",
  "compromisoInicioEn": "2027-02-14T10:00:00-05:00",
  "compromisoFinEn": "2027-02-14T10:30:00-05:00",
  "tiempoServicioMinutos": 5
}
```

Armado temporal (coordenadas ilustrativas; el panel debe enviar las obtenidas del local):

```json
{
  "fecha": "2027-02-14",
  "origen": { "latitud": -1.25, "longitud": -78.62 },
  "salidaProgramadaEn": "2027-02-14T08:00:00-05:00"
}
```

`salidaProgramadaEn` es opcional. Las solicitudes antiguas sin origen conservan el armado anterior para pedidos sin compromiso. Si hay compromisos se requiere origen, sin volver silenciosamente al algoritmo anterior.

## Migración y pruebas

La migración `PlanificacionTemporalRutas` agrega columnas nullable, sin eliminar datos existentes. Aplicar antes de ejecutar esta versión:

```powershell
dotnet ef database update -p Infrastructure -s Infrastructure
dotnet build delivery.slnx -m:1
dotnet test delivery.slnx -m:1
```

Las pruebas PostgreSQL existentes usan `DELIVERY_TEST_CONNECTION` y crean bases temporales aisladas. No se debe apuntar esta variable a un servidor donde el usuario no tenga permiso para crear bases de prueba.

## Límites de esta entrega

Todavía no se ofrecen/reservan intervalos en checkout ni se recalculan rutas operativas con GPS. Los pedidos nuevos con compromiso quedan pendientes para armado temporal en lugar de pasar por la inserción antigua que no comprueba tiempos. Esa inserción tampoco usa rutas que ya tengan planificación temporal o compromisos.

Los ajustes manuales conservan su operación anterior, pero invalidan las estimaciones; iniciar o registrar una entrega también las invalida. Así no se publica una previsión obsoleta como vigente. Validación/replanificación automática ante estos cambios corresponde a etapa 6. No interpretar ausencia de estimaciones como puntualidad validada.

No se introducen APIs de ruteo externas ni se modifica Flutter o Angular. El intervalo estimado de incertidumbre y la oferta de intervalos disponibles quedan para etapas posteriores; en esta entrega se obtiene una hora estimada de finalización con margen configurable.

## Actualización: etapas 5–8

La oferta y reserva de intervalos (`GET /api/entregas/intervalos`, checkout con `compromisoInicioEn`/`compromisoFinEn`), la validación de cambios de ruta (`409 CAMBIO_PONE_EN_RIESGO`, `forzar`), el recálculo por avances y las alertas ya están implementados. Los límites anteriores sobre invalidación de estimaciones quedan reemplazados. Contrato y guía de integración: `docs/frontend/GUIA_FRONTEND_INTERVALOS_ENTREGA.md`. Migración nueva: `IntervalosEntregaReservas`.
