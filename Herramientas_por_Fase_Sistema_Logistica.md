# Herramientas por fase: Sistema de Gestión de Entregas y Rutas

**Materia:** Gestión de Pruebas e Implantación de Software
**Fuente de las herramientas:** APE02, "Selección de tecnologías para pruebas"

Este documento relaciona cada tecnología seleccionada en la APE02 con la fase del proyecto en la que entra y con el entregable del enunciado al que aporta.

---

## 1. Inventario de herramientas

### 1.1 Stack de desarrollo

| # | Herramienta | Componente | Para qué se usa |
|---|---|---|---|
| 1 | **ASP.NET Core (.NET 10)** | Backend / API | API central: usuarios, pedidos, repartidores, asignaciones, rutas y lógica de negocio. |
| 2 | **PostgreSQL 16** | Base de datos | Almacenamiento de toda la información del sistema. |
| 3 | **PostGIS** | Extensión de la BD | Datos geográficos: ubicaciones, zonas (polígonos), distancias. |
| 4 | **Transacciones PostgreSQL** | Concurrencia | Evitar que un mismo pedido se asigne a dos repartidores a la vez. |
| 5 | **SignalR** | Tiempo real | Sincronizar estados de pedidos entre la app móvil y la web. |
| 6 | **Heurística de inserción con capacidad** | Algoritmo de ruteo | Insertar pedidos (incluidos los de último momento) en rutas existentes respetando el máximo por ruta. |
| 7 | **Google OR-Tools (binding C#)** | Algoritmo de ruteo | Resolver el CVRP al armar las rutas del día. |
| 8 | **Angular** | Frontend web | Panel de administración del negocio. |
| 9 | **Leaflet + OpenStreetMap** | Frontend web | Mapas: zonas, ubicaciones y recorridos. |
| 10 | **Flutter** | App móvil | Aplicación de los repartidores. |
| 11 | **geolocator** (paquete Flutter) | App móvil | Obtener y enviar la ubicación del repartidor. |
| 12 | **signalr_netcore** (paquete Flutter) | App móvil | Cliente SignalR para recibir y enviar actualizaciones en tiempo real. |

### 1.2 Herramientas de prueba

| # | Herramienta | Tipo de prueba | Clasificación | Componente que cubre |
|---|---|---|---|---|
| 13 | **SonarCloud / SonarQube** | Análisis estático | Análisis estático | Backend, web y móvil |
| 14 | **xUnit** | Unitarias e integración | Funcional | Backend |
| 15 | **FluentAssertions** | Unitarias | Funcional | Backend |
| 16 | **NSubstitute** | Unitarias (mocks) | Funcional | Backend |
| 17 | **flutter_test** | Unitarias | Funcional | Móvil |
| 18 | **mocktail** | Unitarias (mocks) | Funcional | Móvil |
| 19 | **Jasmine / Karma** | Unitarias | Funcional | Web (Angular) |
| 20 | **Testcontainers** | Integración | Funcional | Backend ↔ PostgreSQL real |
| 21 | **WebApplicationFactory** | Integración | Funcional | API en memoria |
| 22 | **Cypress / Playwright** | Sistema (E2E) | Funcional | Web |
| 23 | **integration_test** (Flutter) | Sistema (E2E) | Funcional | Móvil |
| 24 | **k6** | Volumen, carga y estrés | No funcional | API, BD y SignalR |

---

## 2. Dónde entra cada herramienta (por fase)

### Fase 0: Planificación y análisis de requisitos
**Herramientas:** ninguna técnica. En esta fase **se elige el stack**, y ese es justamente el producto de la APE02.
- Se deja establecido qué herramienta cubre cada tipo de prueba del plan del enunciado (sección 7).

### Fase 1: Diseño del sistema → *Entregable 2*
| Herramienta | Qué se define en esta fase |
|---|---|
| PostgreSQL + PostGIS | Modelo entidad-relación con columnas geográficas (`geometry`/`geography`) para direcciones y polígonos de zona. |
| Transacciones PostgreSQL | Estrategia de concurrencia: nivel de aislamiento, `SELECT … FOR UPDATE` o control optimista. |
| Heurística de inserción + OR-Tools | Diseño y justificación del algoritmo: OR-Tools para el armado masivo (CVRP) y la heurística para los pedidos de último momento. |
| SignalR | Diseño de los *hubs* y eventos, por ejemplo `PedidoActualizado` y `RutaAsignada`. |
| ASP.NET Core | Arquitectura de la API y definición de *endpoints*. |

### Fase 2: Configuración del entorno
| Herramienta | Qué se configura |
|---|---|
| ASP.NET Core, Angular, Flutter | Creación de los tres proyectos en el repositorio. |
| PostgreSQL 16 + PostGIS | Instalación de la BD de desarrollo y activación de la extensión. |
| **SonarCloud / SonarQube** | **Se conecta desde el día 1** para tener un análisis inicial (línea base) y poder mostrar el "antes y después". |
| xUnit, flutter_test, Jasmine/Karma | Proyectos de prueba vacíos, listos para usarse. |
| Testcontainers | Verificar que Docker funcione en las máquinas del equipo. |

### Fase 3: Backend, módulos base
| Herramienta | Uso |
|---|---|
| ASP.NET Core | CRUD de pedidos, zonas y repartidores. |
| PostgreSQL + PostGIS | Validar que la dirección de un pedido caiga dentro de una zona (`ST_Contains`). |
| xUnit + FluentAssertions + NSubstitute | Pruebas unitarias de servicios y validaciones, escritas mientras se desarrolla. |
| SonarCloud | Análisis en cada *push* o *pull request*. |

### Fase 4: Motor de armado de rutas (núcleo)
| Herramienta | Uso |
|---|---|
| Google OR-Tools | Armado de rutas por fecha y zona con capacidad máxima (CVRP). |
| Heurística de inserción | Inserción de pedidos de último momento en rutas ya armadas. |
| PostGIS | Cálculo de distancias entre puntos para la matriz del algoritmo. |
| Transacciones PostgreSQL | Bloqueo durante la asignación para evitar duplicados. |
| xUnit + FluentAssertions + NSubstitute | Pruebas unitarias del algoritmo y de sus casos límite: ruta llena, zona con un solo pedido, sin repartidores disponibles. |
| xUnit + Testcontainers + WebApplicationFactory | Pruebas de integración de **asignaciones simultáneas** contra un PostgreSQL real. |

### Fase 5: Aplicación web (personal del negocio)
| Herramienta | Uso |
|---|---|
| Angular | Pantallas de pedidos, zonas, repartidores y rutas. |
| Leaflet + OpenStreetMap | Mapa con zonas, pedidos y recorridos. |
| SignalR (cliente JS) | Panel de seguimiento en tiempo real. |
| Jasmine / Karma | Pruebas unitarias de componentes y servicios Angular. |

### Fase 6: Aplicación móvil e integración en tiempo real
| Herramienta | Uso |
|---|---|
| Flutter | Pantalla de ruta asignada y cambio de estado del pedido. |
| geolocator | Envío de la ubicación del repartidor. |
| signalr_netcore | Envío y recepción de cambios de estado en tiempo real. |
| SignalR (servidor) | *Hub* que retransmite los cambios a la web. |
| flutter_test + mocktail | Pruebas unitarias de la lógica de entregas en la app. |

### Fase 7: Pruebas funcionales → *Entregables 3 y 4*
| Tipo de prueba | Herramientas | Qué se evidencia |
|---|---|---|
| Análisis estático | SonarCloud / SonarQube | Reporte final con bugs, vulnerabilidades, *code smells* y duplicación, comparado con la línea base de la Fase 2. |
| Unitarias | xUnit + FluentAssertions + NSubstitute · flutter_test + mocktail · Jasmine/Karma | Suite consolidada, resultados y cobertura. |
| Integración | xUnit + Testcontainers + WebApplicationFactory | API ↔ BD, registro → armado → asignación, concurrencia. |
| Sistema (E2E) | Cypress / Playwright (web) · integration_test (Flutter) · SignalR | Flujo completo: pedido registrado → ruta asignada → repartidor marca "entregado" → la web lo refleja. |

### Fase 8: Poblado de datos y pruebas no funcionales → *Entregable 5*
| Tipo de prueba | Herramientas | Qué se mide |
|---|---|---|
| Poblado de datos | PostgreSQL (scripts SQL / `generate_series`) | Miles de pedidos, decenas de repartidores, zonas y rutas históricas; escenario "San Valentín". |
| Volumen | k6 + PostgreSQL | Armado de rutas y consultas geográficas con una BD grande. |
| Carga | k6 | Usuarios concurrentes registrando pedidos y generando rutas: tiempos de respuesta, *throughput* y errores. |
| Estrés | k6 | Aumento progresivo hasta el punto de quiebre del backend, la BD o SignalR, y cómo se recupera el sistema. |

> **Nota:** la APE02 no nombra una herramienta específica para el poblado. Pueden usar scripts SQL directos en PostgreSQL o, si prefieren hacerlo desde C#, la librería **Bogus** (equivalente a Faker para .NET). Conviene agregar la que elijan al informe.

### Fase 9: Informe final → *Entregables 1 y 6*
| Herramienta | Aporte al informe |
|---|---|
| SonarCloud | Métricas de calidad finales. |
| Resultados de xUnit, flutter_test, Jasmine/Karma, Cypress/Playwright | Evidencia de la suite funcional. |
| Reportes de k6 | Hallazgos de rendimiento → mejoras propuestas (índices espaciales en PostGIS, caché, colas, escalamiento de SignalR). |

---

## 3. Vista rápida: herramienta × fase

| Herramienta | F1 | F2 | F3 | F4 | F5 | F6 | F7 | F8 | F9 |
|---|:-:|:-:|:-:|:-:|:-:|:-:|:-:|:-:|:-:|
| ASP.NET Core | ● | ● | ● | ● |   | ● |   |   |   |
| PostgreSQL 16 + PostGIS | ● | ● | ● | ● |   |   |   | ● |   |
| Transacciones PostgreSQL | ● |   |   | ● |   |   | ● |   |   |
| SignalR | ● |   |   |   | ● | ● | ● | ● |   |
| Heurística de inserción + OR-Tools | ● |   |   | ● |   |   |   | ● |   |
| Angular + Leaflet/OSM |   | ● |   |   | ● |   |   |   |   |
| Flutter + geolocator + signalr_netcore |   | ● |   |   |   | ● |   |   |   |
| SonarCloud / SonarQube |   | ● | ● | ● | ● | ● | ● |   | ● |
| xUnit + FluentAssertions + NSubstitute |   | ● | ● | ● |   |   | ● |   | ● |
| flutter_test + mocktail |   | ● |   |   |   | ● | ● |   | ● |
| Jasmine / Karma |   | ● |   |   | ● |   | ● |   | ● |
| Testcontainers + WebApplicationFactory |   | ● |   | ● |   |   | ● |   |   |
| Cypress / Playwright |   |   |   |   |   |   | ● |   | ● |
| integration_test (Flutter) |   |   |   |   |   |   | ● |   | ● |
| k6 |   |   |   |   |   |   |   | ● | ● |

---

## 4. Observaciones

1. **Cypress o Playwright:** la APE02 deja ambas opciones. Conviene elegir **una sola** antes de la Fase 7. Playwright es más rápido y soporta varios navegadores; Cypress tiene una curva de aprendizaje más suave.
2. **SonarCloud desde la Fase 2:** el entregable 3 pide evidencia de las *correcciones aplicadas*, y sin una línea base temprana no hay un "antes" que mostrar.
3. **Concurrencia:** lo ideal es demostrarla dos veces, primero con Testcontainers (prueba de integración controlada, Fase 4/7) y después con k6 (bajo carga real, Fase 8).
4. **k6 y SignalR:** k6 prueba la API HTTP de forma nativa. Para estresar SignalR hay que usar su módulo de WebSockets (`k6/ws` o `k6/experimental/websockets`).
