# Contexto y alcance del Sistema de Gestión de Entregas y Rutas

**Corte:** 13 de noviembre de 2026.  
**Materia:** Gestión de Pruebas e Implantación de Software.

## Propósito

El proyecto organiza entregas de última milla para negocios que reciben pedidos programados y pedidos del mismo día. Busca agrupar entregas por fecha y zona, asignarlas a repartidores disponibles sin exceder su capacidad y mantener al personal informado del avance. El [enunciado](../Enunciado_Proyecto_Sistema_Logistica.md) describe el problema y los entregables académicos; este documento registra el alcance logrado.

## Componentes y estructura

| Componente | Responsabilidad | Estado en esta entrega |
|---|---|---|
| Backend | Reglas de negocio, API, persistencia, planificación, autenticación y eventos | Implementado en este repositorio con ASP.NET Core .NET 10, EF Core, PostgreSQL/PostGIS y SignalR |
| Panel administrativo | Gestionar pedidos, zonas, repartidores y rutas; mostrar indicadores y seguimiento | Equipo Angular en repositorio separado. Se entregó el contrato actualizado para completar la integración con el diseño aprobado |
| Aplicación móvil | Consultar la ruta propia, registrar el avance de las entregas y enviar GPS | Equipo Flutter en repositorio separado. El backend ya proporciona el contrato autenticado para esos flujos |

La solución conserva cuatro capas: `Domain` contiene entidades y reglas; `Application` organiza casos de uso, DTO y validadores; `Infrastructure` implementa PostgreSQL/PostGIS, migraciones y planificación con OR-Tools; `Api` expone controladores, autenticación, Swagger y SignalR. Dentro de cada capa se agrupa por tipo y luego por entidad, por ejemplo `Domain/Entities/Rutas` y `Application/DTOs/Pedidos`. `UnitTests` e `IntegrationTests` contienen las pruebas.

## Alcance implementado

| Área | Resultado | Límite actual |
|---|---|---|
| Acceso | JWT, roles `Administrador` y `Conductor`, gestión de cuentas, cambio obligatorio de credenciales temporales, revocación de sesiones | El conductor solo accede a recursos propios. No hay refresh tokens ni registro público |
| Zonas y repartidores | Alta y consulta de zonas con polígonos, edición de zonas, alta y disponibilidad de repartidores y consulta de su ruta | La ubicación de entrega se valida contra una zona activa |
| Pedidos | Registro, consulta, cancelación y edición de pendientes; destinatario, remitente, correos, observaciones, carga, peso, fragilidad y prioridad | Los nuevos datos son opcionales y compatibles con los contratos anteriores. Peso, fragilidad y prioridad son informativos |
| Planificación | Armado por fecha y zona con OR-Tools; inserción de pedidos del día en una ruta compatible, creación de una nueva si es posible o espera de asignación | La capacidad se mide por cantidad de pedidos. El cálculo usa distancias geográficas, sin recorrido por calles |
| Ajustes de rutas | Asignación manual, transferencia atómica y reordenamiento; control de versiones para edición y nuevos ajustes | Se respetan capacidad, fecha, zona y tramo iniciado. Los POST antiguos aún no ofrecen idempotencia |
| Entregas | Inicio de ruta, pedido en camino, confirmación de entrega y cierre automático al entregar todos los pedidos | La ubicación GPS no cambia por sí sola el estado del pedido |
| GPS | Publicación desde la ruta propia en curso, última ubicación consultable y evento `UbicacionRepartidorActualizada` | Se conserva una posición por repartidor, con fecha y precisión; no hay historial de recorrido |
| Resumen | Indicadores globales filtrados, búsqueda y rango de fechas, independientes de la paginación | El porcentaje entregado excluye cancelados del denominador. No representa puntualidad ni SLA |

### Flujo operativo

1. El administrador configura zonas y repartidores, crea las cuentas y registra pedidos indicando destino, fecha y hora.
2. El backend agrupa pendientes por fecha y zona al solicitar el armado de rutas. Los pedidos del mismo día reciben un resultado de asignación o quedan en espera.
3. El administrador puede ajustar las rutas respetando sus estados y versiones. Si hay un conflicto, el cliente debe recargar antes de confirmar otra modificación.
4. El conductor inicia sesión, cambia sus credenciales temporales si corresponde y consulta su ruta. Desde allí inicia la salida, publica GPS y confirma las entregas.
5. El panel obtiene el estado inicial por REST y recibe `PedidoActualizado`, `RutaActualizada` y `UbicacionRepartidorActualizada` por SignalR. Después de una reconexión, vuelve a consultar los datos y a suscribirse.

## Evidencia y estado de integración

- **Backend:** en la última entrega se verificaron 51 pruebas unitarias, 14 de integración HTTP/SignalR y una adicional contra PostgreSQL aislado: **66 pruebas aprobadas**. Es evidencia de esa entrega, no una ejecución nueva para este documento.
- **Persistencia:** se registró la aplicación local de `Inicial`, `UsuariosAutenticacion` y `AmpliacionPedidosYUbicaciones`. La última migración agrega campos opcionales y la tabla de ubicaciones sin borrar los datos existentes.
- **Contrato:** se exportó OpenAPI `1.1.0` desde el servidor con **36 operaciones** y se verificaron Swagger y el documento OpenAPI.
- **Web y móvil:** el contrato backend y la entrega al equipo web están disponibles. Este repositorio no acredita por sí solo que todas las pantallas nuevas estén integradas ni que el GPS esté validado en un dispositivo físico.

## Pendientes y siguiente hito

El siguiente hito es validar juntos el flujo completo con un administrador, un conductor, datos representativos y un dispositivo móvil: registrar y editar un pedido, planificar o transferir, iniciar ruta, publicar GPS, recibir cambios en la web y confirmar la entrega.

Quedan por acordar o implementar borradores, vehículo y telemetría, ETA y ruteo vial, ventanas horarias y SLA, capacidad por peso, rebalanceo global, exportación masiva, emisiones e idempotencia de operaciones antiguas. Para el cierre académico faltan evidencias consolidadas de análisis estático, pruebas de sistema y pruebas de volumen, carga y estrés con datos representativos. Consultar el [plan de herramientas por fase](../Herramientas_por_Fase_Sistema_Logistica.md).

## Arranque y referencias

Con PostgreSQL/PostGIS y los valores de conexión y firma JWT configurados localmente:

```powershell
dotnet ef database update -p Infrastructure -s Infrastructure
dotnet run --project Api --launch-profile http
```

En desarrollo, Swagger está en `http://localhost:5289/swagger`. Desde otro dispositivo de la red se utiliza la IP real del equipo backend. Las operaciones protegidas requieren `Authorization: Bearer <token>`.

- [Entrega al equipo frontend web](ENTREGA_FRONTEND_WEB.md)
- [OpenAPI 1.1.0](contratos/openapi-1.1.0.json)
- [Autenticación y roles](Autenticacion_JWT_Integracion.md)
- [Reglas y contratos de las ampliaciones](Ampliaciones_Backend_Integracion.md)
- [Solicitud inicial para Flutter](frontend/Solicitud_Flutter_Seguimiento_Repartidores.md)
- Ejemplos HTTP: [`Api/Ampliaciones.http`](../Api/Ampliaciones.http) y [`Api/Autenticacion.http`](../Api/Autenticacion.http).
