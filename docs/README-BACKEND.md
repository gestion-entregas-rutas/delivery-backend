# Documentación del backend

- [Contexto y alcance del proyecto](CONTEXTO_Y_ALCANCE_PROYECTO.md): situación del backend, integración con web y móvil, evidencias y pendientes.

- [Autenticación, roles e integración JWT](Autenticacion_JWT_Integracion.md): acceso vigente para web y Flutter; la API operativa requiere autenticación.
- [Ampliaciones de pedidos, GPS, resumen y ajustes de rutas](Ampliaciones_Backend_Integracion.md): contrato 1.1.0, reglas, ejemplos y migración.
- [OpenAPI 1.1.0 exportado](contratos/openapi-1.1.0.json): instantánea del servidor verificada con 36 operaciones.
- [Retroalimentación del equipo frontend](RETROALIMENTACION_BACKEND.md): necesidades y límites que motivan las ampliaciones.
- [Solicitud original de funcionalidades Flutter](Solicitud_Flutter_Seguimiento_Repartidores.md): contexto inicial; para contratos ya implementados prevalecen las guías JWT y de ampliaciones.

Swagger está en `/swagger` y OpenAPI en `/openapi/v1.json`, disponibles en desarrollo. Las operaciones protegidas requieren `Authorization: Bearer <token>`.
