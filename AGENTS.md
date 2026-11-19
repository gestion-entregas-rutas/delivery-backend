# AGENTS.md — Backend Central (`delivery-backend`)

## 1. Contexto y Misión
Este repositorio es el núcleo central en **C# / .NET** (puerto `5289`, Swagger en `/swagger`, SignalR en `/hubs/seguimiento`).
Actualmente soporta toda la operación de logística de última milla (`Autenticacion`, `Pedidos`, `Zonas`, `Rutas`, `Repartidores`, `Seguimiento`, `Ubicaciones`, `Resumen`).
Tu misión es **ampliar el backend** para soportar el módulo de E-Commerce (Tienda tipo Amazon) definido en `CONTRATO_ECOMMERCE.md`, manteniendo **100% intactos** los contratos existentes de OpenAPI 1.1.0 usados por el panel web y la app móvil Flutter.

## 2. Reglas Inquebrantables de Arquitectura
1. **No romper contratos existentes:** Todos los endpoints actuales de `Pedidos`, `Rutas`, `Zonas`, `Repartidores`, `Seguimiento` y eventos SignalR (`RutaActualizada`, `PedidoActualizado`, `UbicacionRepartidorActualizada`) deben seguir funcionando exactamente igual.
2. **Algoritmos 100% Propios (Cero APIs Externas):**
   - La validación de punto dentro de zona (`Ray Casting`), cálculo de distancias (`Haversine`) y el algoritmo de armado/optimización de rutas (`CVRP` con clusterización por zona, ventanas de tiempo `horaEntrega`, capacidad máxima por ruta e inserción dinámica de pedidos del mismo día) deben estar programados nativamente en C# dentro de la capa de dominio/servicios.
   - No agregar dependencias ni llamadas HTTP a servicios externos de ruteo o geocodificación.
3. **Concurrencia Dual (Crítico para Suite de Pruebas):**
   - **Productos (Admin):** Implementar `RowVersion` / token de concurrencia optimista. Un `PUT /api/productos/{id}` con versión desactualizada debe devolver `HTTP 409 Conflict` (`CONFLICTO_VERSION_PRODUCTO`).
   - **Checkout (Clientes):** Implementar descuento atómico de inventario a nivel de SQL (`UPDATE Productos SET Stock = Stock - @cant WHERE Id = @id AND Activo = 1 AND Stock >= @cant`). Nunca fallar una compra porque otro cliente compró al mismo tiempo si aún queda stock suficiente; si queda insuficiente, hacer rollback total de la transacción y devolver `HTTP 409 Conflict` (`STOCK_AGOTADO`).
   - **Asignación de Rutas:** Mantener bloqueo transaccional/optimista para evitar que dos solicitudes simultáneas asignen el mismo pedido a dos rutas o excedan la capacidad del repartidor.
4. **Puente E-Commerce -> Logística:**
   - Cuando `POST /api/ordenes/checkout` recibe `tipoEntrega == "Domicilio"`, dentro de la misma transacción:
     1. Validar que `(latitud, longitud)` pertenezca a una `Zona` activa usando el algoritmo propio de Ray Casting. Si no tiene cobertura, rechazar antes de cobrar (`HTTP 422`).
     2. Crear la `Orden` y sus `DetalleOrdenes`.
     3. Crear automáticamente un registro en la tabla `Pedidos` con los datos del destinatario, coordenadas, `fechaEntrega`, `horaEntrega`, `detalles` (resumen de productos comprados) y vincular `PedidoId` en la `Orden`.
5. **CORS:** Asegurar que `Program.cs` permita orígenes de desarrollo tanto de `http://localhost:4200` (`frontend-web`) como de `http://localhost:4300` (`frontend-tienda`).

## 3. Plan de Pruebas y Poblado de Datos (Seeder Masivo)
Para cumplir con la materia *Gestión de Pruebas e Implantación de Software*:
1. **Seeder de Catálogo:** Poblar automáticamente las 28 categorías oficiales y al menos 40 productos realistas distribuidos entre ellas con imágenes válidas, ficha técnica y viñetas.
2. **Endpoint o Script de Poblado de Volumen (`POST /api/testing/poblar-escenario-alto-volumen` en entorno Development):**
   - Debe permitir generar rápidamente datos realistas de alta demanda (ej. campaña San Valentín: múltiples zonas, 20+ repartidores, cientos de pedidos programados y del día) para ejecutar pruebas de volumen, carga y estrés (JMeter / k6 / NBomber).
3. **Pruebas Automatizadas (xUnit):**
   - Pruebas unitarias del algoritmo propio de Ray Casting y armado CVRP.
   - Pruebas de integración de concurrencia:
     - Test A: 2 administradores editando el mismo producto -> 1 éxito (`200`), 1 conflicto (`409`).
     - Test B: Stock = 100, 2 clientes comprando 50 en paralelo (`Task.WhenAll`) -> ambos `201`, stock final = 0.
     - Test C: Stock = 50, 2 clientes comprando 40 en paralelo (`Task.WhenAll`) -> uno `201`, uno `409 STOCK_AGOTADO`, stock final = 10 (nunca negativo).

## 4. Verificación antes de finalizar
- Ejecutar `dotnet build` sin advertencias críticas ni errores de análisis estático.
- Ejecutar `dotnet test` asegurando que todas las pruebas unitarias y de concurrencia pasen al 100%.
- Verificar que Swagger (`/swagger`) exponga tanto los endpoints originales como los nuevos de E-Commerce.