# Revisión del backend frente al enunciado

Fecha: 16 de octubre de 2026.

## Alcance y arquitectura

Se revisaron entidades, servicios de aplicación, controladores, persistencia,
planificador, notificaciones y pruebas existentes. Se conserva la arquitectura
en capas: Api expone HTTP y SignalR; Application coordina operaciones y DTO;
Domain contiene entidades, transiciones y reglas; Infrastructure implementa
persistencia PostgreSQL/PostGIS y planificación OR-Tools. No se propone una
reestructuración de los proyectos.

Esta revisión del código y las pruebas locales no certifica el funcionamiento
de las aplicaciones web y móvil ni la capacidad del sistema en producción.

## Matriz de requisitos

| Requisito | Evidencia en backend | Resultado |
| --- | --- | --- |
| Registrar pedidos anticipados con fecha, hora y dirección | Pedido.Registrar, PedidoService, PedidosController | Implementado; distingue RegistradoEn y FechaEntrega y rechaza fechas pasadas. |
| Definir zonas y agrupar geográficamente | Zona, ZonaService, polígonos PostGIS | Implementado; resolución automática por pertenencia a zona activa. |
| Armar rutas por fecha y zona | RutaService.ArmarRutasAsync | Implementado mediante acción manual; no es obligatorio tener además un proceso programado. |
| Respetar capacidad por salida | Ruta.AgregarPedido y restricción de capacidad OR-Tools | Implementado con el mínimo entre capacidad del repartidor y máximo de la zona. Corrección de concurrencia descrita abajo. |
| Recibir pedidos el mismo día | AsignadorUltimoMomento | Inserción por menor distancia adicional; crea ruta si hay repartidor libre o conserva el pedido pendiente. |
| Gestionar repartidores y consultar ruta | RepartidorService, RepartidoresController | Implementado: registro, disponibilidad y consulta por fecha. |
| Ajustar rutas manualmente | RutaService, RutasController | Implementado: agregar y quitar pedidos y cancelar rutas planificadas. |
| Seguimiento de estados | Pedido, SeguimientoService | Parcial: pendiente, asignado, en camino y entregado; falta un estado/acción explícito de recogida mencionado en el módulo de seguimiento. |
| Reflejar cambios inmediatamente | SeguimientoHub, NotificadorSignalR | Mecanismo implementado. Se corrigieron eventos ausentes al asignar pedidos. Falta comprobar el flujo entre clientes reales. |
| Evitar conflictos simultáneos | xmin, índices únicos y reintentos de última hora | Hay defensas implementadas. Se corrigió la actualización de versión del agregado Ruta. Falta ejecutar carreras reales contra PostgreSQL. |
| Justificar algoritmo | PlanificadorRutasOrTools e InsercionRutas | Documentado debajo, con limitaciones. |
| Pruebas unitarias | UnitTests | Suite ejecutable; incluye regresiones nuevas. |
| Pruebas de integración | IntegrationTests/UnitTest1.cs | Pendiente: contiene una prueba vacía que no verifica comportamiento. |
| Pruebas del sistema web/móvil | No se encontraron en esta solución | Pendiente de implementar y ejecutar con las aplicaciones. |
| Volumen, carga y estrés con datos abundantes | No se encontraron suites ni resultados en esta solución | Pendiente de generar datos, definir escenarios, ejecutar y documentar resultados. |
| Evidencias de análisis estático | Compilación local con advertencias tratadas como errores | Verificación básica realizada; no equivale a un informe completo de análisis estático ni a evidencias de una herramienta externa. |

## Correcciones aplicadas

1. **Concurrencia del agregado Ruta.** Agregar una parada podía modificar solamente
   Pedido y ParadaRuta sin actualizar Ruta. Dos solicitudes sobre pedidos distintos
   podían leer el último cupo disponible y ambas guardarlo, porque el xmin de Ruta
   no participaba. AppDbContext ahora fuerza una actualización de la ruta ya
   persistida cuando cambian sus paradas o pedidos asociados cargados. PostgreSQL
   comprueba su xmin dentro del guardado transaccional de EF. Se reutiliza una
   columna existente con su mismo valor; no requiere migración. La protección
   también evita que dos entregas concurrentes dejen la ruta sin completar al
   evaluar cada una una versión anterior de los pedidos.
2. **Notificación de asignaciones.** El armado de rutas y la incorporación manual
   notifican ahora PedidoActualizado además de RutaActualizada después de guardar,
   para que las suscripciones de seguimiento del pedido reciban su nuevo estado.
3. **Inserción con entregas fuera de orden.** La posición mínima ahora se calcula
   desde el mayor Orden de una parada en camino o entregada. Contarlas permitía
   insertar antes de una parada avanzada cuando existían huecos anteriores.

Las pruebas nuevas comprueban la protección del tramo avanzado y la participación
de Ruta en el control de versión de EF al agregar/quitar paradas o cambiar estados.
La inspección de EF no sustituye una prueba concurrente con PostgreSQL real.

## Algoritmo y límites observados

El planificador usa OR-Tools para un problema de ruteo con capacidad, por zona y
fecha. Calcula distancias geográficas, utiliza un centro promedio como depósito
virtual y penaliza dejar pedidos sin asignar. La búsqueda tiene un tiempo límite.
La inserción de última hora minimiza la distancia adicional de una parada.

HoraEntrega se registra y ordena al consultar pendientes, pero el optimizador no
impone ventanas horarias. Por eso no se puede afirmar que el orden optimizado
respete las horas deseadas. El enunciado sugiere investigar ventanas de tiempo;
no define tolerancias, velocidades ni tiempos de servicio. Hay que concretar esas
reglas antes de prometer cumplimiento horario. Tampoco se calculan distancias por
calles ni se utiliza un depósito físico configurado.

La matriz de distancias crece cuadráticamente con el número de pedidos de una
zona. El comportamiento con miles de pedidos requiere medición, especialmente
antes de decidir si conviene particionar el trabajo.

Los pedidos en espera pueden procesarse mediante un nuevo armado manual. El
armado por lotes crea rutas nuevas y no rellena rutas previas; la inserción en
rutas existentes se realiza en el flujo de último momento o mediante ajuste manual.

Las fechas locales dependen de la zona horaria del servidor (TimeProvider.System).
Debe verificarse su coincidencia con la del negocio al desplegar.

## Trabajo necesario para completar la evidencia del enunciado

1. Incorporar recogida explícita y definir qué ocurre cuando una ruta ya salió y
   recibe un pedido nuevo: el enunciado permite inserción, pero no define cómo lo
   recoge físicamente el repartidor.
2. Sustituir la prueba vacía por integración con PostgreSQL/PostGIS y API:
   persistencia, transiciones, errores y asignaciones simultáneas. Verificar que
   no hay sobrecupo ni pedidos duplicados y que se libera el repartidor al finalizar.
3. Ejecutar pruebas de sistema del flujo negocio/repartidor, incluida recepción
   de eventos SignalR y actualización de vistas.
4. Preparar un poblador reproducible de zonas, repartidores, rutas y pedidos;
   ejecutar volumen, carga y estrés con un escenario de alta demanda y conservar
   métricas, parámetros, datos iniciales y resultados reales.
5. Completar el documento de diseño con flujo de datos, obtener las evidencias de
   análisis estático y redactar el informe final de hallazgos y mejoras.

No se declara cumplimiento total mientras estas verificaciones y entregables
permanezcan pendientes.

## Verificación local

- `dotnet test UnitTests/UnitTests.csproj --no-restore`: 31 pruebas correctas.
- `dotnet build delivery.slnx --no-restore -warnaserror`: compilación verificada.
- `git diff --check`: sin errores de espacios en los cambios versionados.
- No se ejecutaron pruebas de carga ni carreras contra una base PostgreSQL real.
