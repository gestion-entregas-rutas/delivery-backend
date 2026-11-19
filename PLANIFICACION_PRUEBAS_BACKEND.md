# Planificación de Pruebas — Backend (delivery-backend)

## 1. Contexto del proyecto (parte evaluada)
- **Sistema**: núcleo backend en **C#/.NET** para logística de última milla extendido con **E-Commerce** integrado (catálogo, carrito, órdenes/pagos simulados, reseñas) manteniendo la operación logística existente.
- **Alcance técnico**: API REST (Swagger/OpenAPI), SignalR (`/hubs/seguimiento`), PostgreSQL + PostGIS, EF Core, autenticación/autorización por roles (`Administrador`, `Conductor`, `Cliente`).
- **Contratos preservados**: endpoints/eventos de `Pedidos`, `Rutas`, `Zonas`, `Repartidores`, `Seguimiento/Ubicaciones`, `Resumen` y eventos `RutaActualizada`, `PedidoActualizado`, `UbicacionRepartidorActualizada` permanecen operativos.
- **Reglas clave implementadas**:
  - Algoritmos **100% propios en C#** (sin librerías/APIs externas de ruteo/geocodificación): **Ray Casting** (cobertura de punto en zona), **Haversine** (distancia geodésica) y **planificador CVRP propio** (armado/optimización de rutas con capacidad y ventanas de tiempo).
  - **Concurrencia**: edición de producto con versión optimista (`409 CONFLICTO_VERSION_PRODUCTO`) y descuento de stock **atómico en SQL** en checkout (`409 STOCK_AGOTADO` con `productoId/stockDisponible`, sin stock negativo).
  - **Puente e-commerce → logística**: en entrega a domicilio, al confirmar pago se valida cobertura por zona activa (ray casting) y se crea transaccionalmente la `Orden` y el `Pedido` logístico vinculado.
  - **Pagos simulados** desacoplados (`IPagoService`), ciclo reserva → confirmación/cancelación/expiración con idempotencia (`Idempotency-Key`).
- **Componentes y ubicaciones relevantes**:
  - Ray casting: `Domain/Entities/Zonas/Zona.cs` (`Contiene`).
  - Haversine: `Domain/ValueObjects/GeoPoint.cs` (`DistanciaKm`).
  - CVRP propio: `Infrastructure/ExternalServices/Rutas/PlanificadorRutasPropio.cs` (DI en `Infrastructure/DependencyInjection.cs`).
  - Catálogo/concurrencia producto: `Application/Services/Catalogo/CatalogoService.cs`.
  - Carrito/órdenes/pagos/stock atómico/idempotencia: `Application/Services/Carrito/*`, `Application/Services/Ordenes/OrdenService.cs`.
  - Poblado masivo (dev): `POST /api/testing/poblar-escenario-alto-volumen` (`Api/Controllers/Testing/TestingController.cs`).
- **Pruebas automatizadas existentes (referencia)**:
  - Unitarias: `UnitTests/Domain/Zonas/ZonaRayCastingTests.cs`, `UnitTests/Infrastructure/Rutas/PlanificadorRutasOrToolsTests.cs` (planificador propio).
  - Integración/PG: `IntegrationTests/PostgreSQL/AmpliacionesPostgresTests.cs`, `IntegrationTests/PostgreSQL/Etapa6PostgresTests.cs` (concurrencia A–D y flujos, `[PostgresFact]`).

## 2. Análisis de requisitos (qué debe probarse)
| ID Req | Módulo | Requisito / funcionalidad a verificar | Prioridad |
|---|---|---|---|
| RQ-ALG-01 | Algoritmos | Ray casting: determinar correctamente si un punto está dentro/fuera de un polígono de zona (incl. polígonos cóncavos y bordes de cobertura). | Alta |
| RQ-ALG-02 | Algoritmos | Haversine: cálculo consistente de distancias geográficas usado en decisiones de ruteo/proximidad. | Alta |
| RQ-ALG-03 | Algoritmos | CVRP propio: asignar cada pedido una sola vez, respetar `min(zona.MaxPedidosPorRuta, repartidor.CapacidadMaxima)`, considerar `HoraEntrega` (ventanas/orden), operar por zona y con/sin recursos. | Alta |
| RQ-CAT-01 | Catálogo | Listado/filtrado/búsqueda/paginación de productos y visibilidad (ocultar inactivos o con categoría/subcategoría inactiva al público). | Alta |
| RQ-CAT-02 | Catálogo | Concurrencia optimista en edición admin de producto: versión vigente → éxito; versión obsoleta → `409 CONFLICTO_VERSION_PRODUCTO`. | Alta |
| RQ-CAT-03 | Catálogo | Reseñas: solo clientes que compraron, una por producto, calificación válida y actualización de promedio/conteo. | Media |
| RQ-CAR-01 | Carrito | Carrito invitado (token) y autenticado, operaciones CRUD de ítems, merge (suma/acota a stock) y cotización/revalidación. | Alta |
| RQ-ORD-01 | Checkout | Reserva atómica de stock (todo-o-nada), validaciones de datos, idempotencia por `Idempotency-Key`. | Alta |
| RQ-ORD-02 | Checkout | Cobertura domicilio por ray casting: dentro de zona activa → permitido; sin cobertura → `422`. | Alta |
| RQ-ORD-03 | Pagos | Ciclo reserva `PendientePago` → confirmar/cancelar/expirar; liberación de stock; confirmación tardía rechazada (`409 PAGO_NO_CONFIRMABLE`); confirmaciones duplicadas idempotentes. | Alta |
| RQ-ORD-04 | Puente logístico | Confirmación de domicilio crea/vincula `Pedido` logístico transaccionalmente; retiro en tienda gestiona `codigoRetiro` y marcado de retirado (admin). | Alta |
| RQ-CONC-01 | Concurrencia | Escenarios A–D: edición concurrente de producto, compras paralelas con/sin stock suficiente, compra vs edición simultánea sin restaurar/perder descuento. | Crítica |
| RQ-LOG-01 | Regresión logística | Contratos y flujos existentes de pedidos/rutas/zonas/repartidores/seguimiento/SignalR siguen funcionando. | Alta |
| RQ-NFR-01 | Datos/volumen | Poblado masivo en desarrollo y comportamiento estable ante alta demanda; integridad referencial y consistencia transaccional. | Media |

## 3. Diseño de pruebas (matriz técnica)
> **Convenciones**: Resultado esperado = estado observable (HTTP/código/estado BD/evento). Criterio de aceptación = condición binaria para aprobar. Evidencias: capturas/logs/respuestas JSON/consultas SQL/reportes de `dotnet test`.

### 3.1 Algoritmos (foco principal)

#### 3.1.1 Ray casting — cobertura de zona (`Zona.Contiene`)
| ID | Requisito | Tipo | Precondiciones | Datos de entrada | Pasos | Resultado esperado | Criterio de aceptación | Prioridad | Evidencia |
|---|---|---|---|---|---|---|---|---|---|
| ALG-RC-01 | RQ-ALG-01 | Unitaria | Zona con polígono convexo definido | Punto interior (ej. centroide/interior) | Invocar `Contiene(punto)` | Retorna `true` | Punto claramente interno clasificado como contenido | Alta | Resultado xUnit / log |
| ALG-RC-02 | RQ-ALG-01 | Unitaria | Misma zona | Punto exterior alejado | Invocar `Contiene(punto)` | Retorna `false` | Punto externo no contenido | Alta | Resultado xUnit |
| ALG-RC-03 | RQ-ALG-01 | Unitaria | Zona con **polígono cóncavo** (ej. forma “L”) | Puntos: (a) en brazo/base interna, (b) en hueco cóncavo, (c) fuera | Invocar `Contiene` por cada punto | (a) `true`, (b) `false`, (c) `false` | Clasificación correcta en concavidad (caso crítico de ray casting) | Crítica | `ZonaRayCastingTests` / captura |
| ALG-RC-04 | RQ-ALG-01 | Unitaria (borde) | Polígono válido | Punto sobre vértice o arista (si aplica al modelo) | Invocar `Contiene` | Comportamiento determinista documentado (consistente con implementación) | Sin excepciones; resultado consistente y conocido | Media | Log de ejecución |
| ALG-RC-05 | RQ-ALG-01 | Integración/API | Zona activa en BD; endpoint de checkout domicilio | Coordenadas dentro y fuera de zona | Ejecutar `POST /api/ordenes/checkout` (domicilio) | Dentro: reserva/flujo válido; fuera: **`422`** sin crear orden/pedido ni descontar stock indebido | Cobertura aplicada en regla de negocio real | Alta | Respuesta HTTP + estado BD |

#### 3.1.2 Haversine — distancia geográfica (`GeoPoint.DistanciaKm`)
| ID | Requisito | Tipo | Precondiciones | Datos de entrada | Pasos | Resultado esperado | Criterio de aceptación | Prioridad | Evidencia |
|---|---|---|---|---|---|---|---|---|---|
| ALG-HV-01 | RQ-ALG-02 | Unitaria | Coordenadas válidas | Mismo punto (A,A) | Calcular distancia | ~0 km | Resultado ≈ 0 (tolerancia numérica) | Alta | xUnit/assert |
| ALG-HV-02 | RQ-ALG-02 | Unitaria | Coordenadas válidas | Par de puntos con distancia conocida/aproximada | Calcular distancia | Valor dentro de tolerancia esperada | Error absoluto/relativo aceptable para ruteo | Alta | xUnit/assert |
| ALG-HV-03 | RQ-ALG-02 | Unitaria | Coordenadas válidas | (A,B) y (B,A) | Calcular en ambos sentidos | Igualdad de resultados | Simetría cumplida | Media | xUnit/assert |

#### 3.1.3 Planificador CVRP propio (`PlanificadorRutasPropio`)
| ID | Requisito | Tipo | Precondiciones | Datos de entrada | Pasos | Resultado esperado | Criterio de aceptación | Prioridad | Evidencia |
|---|---|---|---|---|---|---|---|---|---|
| ALG-CV-01 | RQ-ALG-03 | Unitaria | Zona, N pedidos pendientes, M repartidores con capacidad suficiente | Pedidos con ubicaciones/`HoraEntrega`, capacidades definidas | Invocar `Planificar(zona, pedidos, repartidores)` | Cada pedido asignado **una sola vez**; rutas con `count <= min(zona.MaxPedidosPorRuta, rep.CapacidadMaxima)` | 0 duplicados, 0 omisiones con capacidad suficiente, 0 sobrecapacidad | Crítica | `PlanificadorRutasPropioTests` |
| ALG-CV-02 | RQ-ALG-03 | Unitaria | Zona `MaxPedidosPorRuta` > capacidad repartidor | Repartidor con capacidad menor | Planificar | Ninguna ruta excede capacidad efectiva del repartidor | Se respeta el mínimo entre zona y repartidor | Crítica | xUnit |
| ALG-CV-03 | RQ-ALG-03 | Unitaria | Pedidos con distintas `HoraEntrega` | Conjunto desordenado temporalmente | Planificar | Secuencia por ruta en orden **no decreciente** de `HoraEntrega` (ventana/precedencia) | Orden temporal respetado por ruta | Alta | xUnit |
| ALG-CV-04 | RQ-ALG-03 | Unitaria | Sin pedidos o sin repartidores | Listas vacías correspondientes | Planificar | Lista vacía, sin excepciones | Manejo seguro de casos límite | Alta | xUnit |
| ALG-CV-05 | RQ-ALG-03 | Unitaria/carga | Alta demanda (cientos de pedidos, 20+ repartidores, múltiples zonas vía armado por zona) | Datos generados (poblador dev o fixtures) | Planificar por zona(s) | Asignaciones válidas, sin duplicados, sin exceder capacidades, tiempo de ejecución razonable en dev | Corrección mantenida bajo volumen | Alta | Log/tiempo/resultado |
| ALG-CV-06 | RQ-ALG-03, RQ-LOG-01 | Integración | Rutas/pedidos pendientes en BD, planificador registrado en DI | Invocación de armado de rutas (servicio/controlador existente) | Ejecutar flujo de armado | Rutas creadas coherentes con restricciones; eventos/estados logísticos consistentes | Paridad funcional del ruteo sin OR-Tools | Alta | BD + eventos/respuesta |

### 3.2 E-Commerce, concurrencia y flujos

| ID | Requisito | Tipo | Precondiciones | Datos de entrada | Pasos | Resultado esperado | Criterio de aceptación | Prioridad | Evidencia |
|---|---|---|---|---|---|---|---|---|---|
| CAT-01 | RQ-CAT-01 | Integración/API | Catálogo sembrado | Filtros `categoriaId/buscar/precio/soloOfertas/orden/paginación` | `GET /api/productos` | Resultados filtrados/paginados; sin productos/categorías/subcategorías inactivas para público | Coincidencia exacta con filtros y visibilidad | Alta | Respuesta JSON |
| CAT-02 | RQ-CAT-02 | Integración/concurrencia | Producto con `version` V1 | 2 `PUT` simultáneos con misma `version` | Ejecutar en paralelo | 1 **200** con nueva versión, 1 **409** `CONFLICTO_VERSION_PRODUCTO` | Solo una actualización persiste; conflicto informado | Crítica | Test auto/log HTTP |
| CAR-01 | RQ-CAR-01 | Integración | Producto activo con stock | Invitado: agregar ítems con `X-Cart-Token`; luego login | `POST /api/carrito/items`, `POST /api/carrito/merge`, `GET /api/carrito/cotizacion` | Carrito consolidado (suma/acotado a stock), cotización con totales/advertencias coherentes | Estado final correcto y sin pérdida indebida de ítems | Alta | Respuestas JSON |
| ORD-01 | RQ-ORD-01, RQ-CONC-01 (B) | Concurrencia/Integración (PG) | Stock=100 | 2 checkouts paralelos de 50 (mismo producto) con `Idempotency-Key` distintos | Ejecutar en paralelo | Ambos **201**, stock final **0** | Descuento atómico sin sobreventa | Crítica | `Etapa6PostgresTests`/BD |
| ORD-02 | RQ-ORD-01, RQ-CONC-01 (C) | Concurrencia/Integración (PG) | Stock=50 | 2 checkouts paralelos de 40 | Ejecutar en paralelo | Uno **201**, otro **409 `STOCK_AGOTADO`** (`productoId`,`stockDisponible`), stock final **10** | Nunca negativo; fallo total del intento insuficiente | Crítica | Test/BD |
| ORD-03 | RQ-ORD-02 | Integración/API | Zonas activas cargadas | Domicilio con coords dentro y fuera de zona | `POST /api/ordenes/checkout` | Dentro: **201 `PendientePago`**; fuera: **`422`** | Cobertura validada antes de confirmar cobro | Alta | HTTP + BD |
| ORD-04 | RQ-ORD-03 | Integración/API | Orden `PendientePago` válida | `confirmar` (misma `Idempotency-Key`/reintento) y `cancelar` | Llamadas repetidas/controladas | Confirmación idempotente; cancelación libera stock; expirada/tardía → **409 `PAGO_NO_CONFIRMABLE`** | Estados y stock consistentes ante duplicados/fallos | Crítica | HTTP + BD |
| ORD-05 | RQ-ORD-04 | Integración/API | Checkout domicilio confirmado | Confirmar pago domicilio | `POST /api/ordenes/{id}/pago/confirmar` | Orden pagada y **`Pedido` logístico creado/vinculado** con datos/coordenadas/fecha-hora/detalles; retiro crea `codigoRetiro` y flujo admin de retirado | Puente e-commerce→logística correcto | Crítica | BD (Orden/Pedido) |
| ORD-06 | RQ-CONC-01 (D) | Concurrencia/Integración (PG) | Stock conocido, versión de producto | Compra (reserva) y edición admin simultáneas | Ejecutar en paralelo | Stock final refleja solo descuentos reales (sin restaurar ni perder); edición exitosa o en conflicto sin revertir compra válida | Consistencia ante operaciones mixtas | Crítica | Test/BD |
| RES-01 | RQ-CAT-03 | Integración/API | Cliente con compra del producto | `POST /api/productos/{id}/resenas` | Enviar calificación/comentario | Reseña creada, promedio/conteo actualizados; duplicado o no comprador rechazado | Reglas de negocio de reseñas cumplidas | Media | HTTP + BD |
| REG-01 | RQ-LOG-01 | Regresión/Integración | Datos logísticos | Flujos existentes de rutas/pedidos/seguimiento/SignalR | Ejecutar pruebas/escenarios previos | Sin cambios de contrato ni fallos nuevos | Regresión cero en logística | Alta | Reporte `dotnet test` |

## 4. Preparación del entorno de pruebas
| Elemento | Detalle |
|---|---|
| **Herramientas** | .NET SDK 10.x, `dotnet` CLI, PostgreSQL 16+ con **PostGIS**, (opcional) `dotnet-ef`, cliente HTTP (Swagger/Postman/curl), navegador. |
| **Configuración** | `ConnectionStrings:DefaultConnection` válida; entorno `Development` para Swagger y endpoint de poblado masivo. |
| **Variables** | `DELIVERY_TEST_CONNECTION` apuntando a instancia PostgreSQL con permisos de **crear/eliminar** bases (para pruebas `[PostgresFact]` que crean BD temporales `delivery_test_*`). |
| **Datos base** | Migraciones aplicadas (`dotnet ef database update ...`); seed automático en desarrollo (28 categorías, 40+ productos) o `POST /api/testing/poblar-escenario-alto-volumen` para volumen logístico. |
| **Usuarios/roles** | Administrador, Conductor y Cliente (registro/login) con tokens JWT válidos; definir `Idempotency-Key` por intento en pruebas de checkout/confirmación. |
| **Aislamiento** | Para concurrencia y repetibilidad, preferir base de datos limpia/temporal o restaurar estado (stock/órdenes) entre casos. |

## 5. Ejecución de pruebas (plan)
| Fase | Actividad | Responsable (rol) | Frecuencia/orden | Salida |
|---|---|---|---|---|
| E1 | **Smoke y build**: `dotnet build delivery.slnx` | Desarrollador/QA técnico | Inicial y tras cambios | Build sin errores |
| E2 | **Unitarias (algoritmos y dominio)**: `dotnet test UnitTests/UnitTests.csproj` (ray casting, CVRP, etc.) | Desarrollador/QA técnico | Por commit/etapa | Reporte xUnit (verde) |
| E3 | **Integración no-PG / existentes**: `dotnet test IntegrationTests/IntegrationTests.csproj` | QA técnico | Por etapa | Reporte (ver skips de PG si no hay env) |
| E4 | **Integración y concurrencia PostgreSQL**: configurar `DELIVERY_TEST_CONNECTION` y ejecutar pruebas `[PostgresFact]` (A–D, flujos, ampliaciones) | QA técnico | Antes de entrega/evaluación | Evidencia de consistencia transaccional |
| E5 | **Pruebas funcionales API (manuales/exploratorias guiadas)**: catálogo, carrito, checkout/confirm/cancel (retiro y domicilio, cobertura, stock), reseñas, retiros admin | QA funcional | Tras E2–E4 | Matriz diligenciada + capturas |
| E6 | **Regresión logística**: rutas/pedidos/zonas/repartidores/ubicaciones/SignalR y escenarios alto volumen | QA funcional/técnico | Antes de cierre | Confirmación de no regresión |

## 6. Registro de incidentes (defectos)
> Usar un registro único (hoja/tabla) durante la ejecución; cada hallazgo debe ser reproducible.

| ID Defecto | Fecha | Caso/ID prueba | Módulo | Severidad (Crítica/Alta/Media/Baja) | Prioridad | Entorno/Build | Pasos para reproducir | Resultado obtenido | Resultado esperado | Evidencia (log/captura/request) | Estado | Responsable |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| DEF-001 |  |  |  |  |  |  |  |  |  |  | Abierto/En curso/Resuelto/Cerrado |  |

**Criterios de severidad (guía)**: *Crítica* (sobreventa, corrupción de stock/pedidos, ruptura transaccional, falla de cobertura que permita entrega inválida), *Alta* (funcionalidad clave no cumple contrato/409-422 incorrectos), *Media* (caso borde/UX de error), *Baja* (cosmético/no bloqueante).

## 7. Verificación de resultados y criterios de salida
- **Por caso**: `Resultado real == Resultado esperado` y se cumple el **criterio de aceptación**; en BD no hay inconsistencias (stock ≥ 0, sin pedidos/órdenes huérfanas fuera de lo definido, estados coherentes).
- **Automatización**: suites ejecutadas en verde según entorno (`UnitTests` siempre; `PostgresFact` al contar con `DELIVERY_TEST_CONNECTION`).
- **Criterios de salida (exit)**:
  - 100% de casos **Críticos/Altos** aprobados; defectos críticos/altos cerrados o con mitigación aceptada y documentada.
  - Escenarios de **algoritmos** (ray casting, CVRP capacidad/HoraEntrega, Haversine) y **concurrencia A–D** aprobados.
  - Sin regresiones en contratos logísticos existentes.
  - Evidencias completas y trazables (matriz, reportes, capturas/logs).

## 8. Entrega de evidencias
- **Documento fuente (raíz del proyecto)**: `PLANIFICACION_PRUEBAS_BACKEND.md` (este documento) y, durante ejecución, matriz diligenciada (puede exportarse a Excel/Word) y registro de defectos.
- **Evidencias a adjuntar**:
  - Reportes de `dotnet build` y `dotnet test` (consola/archivo), indicando fecha, commit/versión y entorno.
  - Capturas/respuestas JSON de casos API clave (409 `CONFLICTO_VERSION_PRODUCTO`/`STOCK_AGOTADO`, 422 cobertura, checkout/confirmación idempotente, creación de pedido logístico).
  - Consultas/estado en BD para escenarios de concurrencia y consistencia (stock, versiones, órdenes/pagos/pedidos).
  - Resultados específicos de algoritmos (xUnit de ray casting/CVRP y, si se ejecutan, mediciones de volumen con poblador dev).
- **Formato final solicitado**: generar **PDF** basado en el formato indicado a partir de la evidencia consolidada (y mantener el insumo en Word/Markdown en la raíz para orden y trazabilidad).
- **Trazabilidad**: relacionar cada evidencia con `ID` de caso (ALG-*, CAT-*, ORD-*, REG-*) y, cuando aplique, con el test automatizado correspondiente.

---
**Notas**: el pago es **simulado** y desacoplado para futura pasarela; las pruebas de carga masiva se apoyan en el endpoint de desarrollo y en el planificador/consultas propios. Ajustar datos (IDs, tokens, coordenadas de zona) al entorno al momento de ejecutar.