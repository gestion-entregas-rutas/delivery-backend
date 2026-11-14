# CONTRATO MAESTRO E-COMMERCE & LOGÍSTICA (v2.0.0)

**Propósito:** Contrato único de verdad entre `delivery-backend` (.NET/C#), `frontend-tienda` (Angular Cliente) y `frontend-web` (Angular Admin RouteOS). Ningún agente debe inventar rutas, nombres de propiedades JSON ni códigos de error fuera de este documento.

---

## 1. Arquitectura Integrada (Tienda + Última Milla)

El sistema opera como una tienda en línea completa (estilo Amazon) conectada directamente al motor logístico existente:
1. **Navegación Pública (Invitado):** Cualquier usuario puede explorar las 28 categorías, buscar productos, ver fichas técnicas/reseñas y gestionar su carrito localmente sin iniciar sesión.
2. **Autenticación al Pagar:** Al pulsar "Proceder al pago" en el carrito, si no hay JWT válido con rol `Cliente`, se exige iniciar sesión o registrarse (`POST /api/autenticacion/registro-cliente`), conservando el carrito intacto.
3. **Checkout y Bifurcación de Entrega:**
   - **`RetiroTienda`:** El cliente ingresa cédula, nombre completo de quien retira y fecha/franja horaria de retiro. Se genera la orden pagada con código de retiro; **NO** crea pedido de despacho ni entra en rutas de repartidores.
   - **`Domicilio`:** El cliente selecciona punto en mapa dentro de una zona activa (validación ray casting). Al confirmarse el pago, el backend crea **transaccionalmente** tanto la `Orden` como el `Pedido` en el módulo logístico (`Estado: Pendiente` o `EnEspera` según fecha), listo para el algoritmo propio de armado de rutas.

---

## 2. Catálogo Oficial de Departamentos (28 Categorías)

El backend debe poblar (Seed) exactamente estas 28 categorías principales:
1. `Todos los departamentos` (filtro virtual en frontend, ID 0 o sin filtro)
2. `Arte y artesanías`
3. `Automotriz`
4. `Bebé`
5. `Belleza y cuidado personal`
6. `Cine y TV`
7. `Computadoras`
8. `Deportes y actividades al aire libre`
9. `Electrónicos`
10. `Equipaje`
11. `Herramientas y mejoramiento del hogar`
12. `Hogar y cocina`
13. `Industria y Ciencia`
14. `Insumos para mascotas`
15. `Juguetes y juegos`
16. `Libros`
17. `Moda para Hombre`
18. `Moda para Mujer`
19. `Moda para Niñas`
20. `Moda para Niños`
21. `Música MP3`
22. `Música, CDs y Vinilos`
23. `Ofertas`
24. `Prime Video`
25. `Salud y Hogar`
26. `Software`
27. `Tienda Kindle`
28. `Videojuegos`

---

## 3. Modelo de Producto (Completo y Limpio)

No usar variantes complejas de color/talla ni cálculos arancelarios.

```json
{
  "id": "uuid",
  "categoriaId": 12,
  "categoriaNombre": "Hogar y cocina",
  "subcategoria": "Velas y Portavelas",
  "nombre": "Vela perfumada de manzana y canela azucarada, tarro grande",
  "marca": "Yankee Candle",
  "descripcionCorta": "Hasta 150 horas de tiempo de combustión, fragancia de relleno de habitación.",
  "precioBase": 19.99,
  "precioOferta": 14.99,
  "stock": 100,
  "activo": true,
  "imagenPrincipalUrl": "https://...",
  "galeriaMultimedia": [
    { "tipo": "imagen", "url": "https://...", "orden": 1 },
    { "tipo": "video", "url": "https://...", "orden": 2 }
  ],
  "fichaTecnica": [
    { "clave": "Marca", "valor": "Yankee Candle" },
    { "clave": "Dimensiones", "valor": "4\" an. x 6.8\" al. pulgadas" },
    { "clave": "Peso", "valor": "2.52 libras" },
    { "clave": "Material", "valor": "Cera vegetal y mecha de fibra natural" },
    { "clave": "Cantidad por paquete", "valor": "1" }
  ],
  "sobreEsteArticulo": [
    "Manzana de canela azucarada: delicioso aroma de rodajas de manzana crujientes.",
    "Vela de larga duración con hasta 150 horas de fragancia."
  ],
  "calificacionPromedio": 4.6,
  "totalResenas": 128,
  "version": "AAAAAAAAF3E="
}
```

---

## 4. Reglas Críticas de Concurrencia (Obligatorias para Pruebas)

1. **Edición Simultánea de Productos por Administradores (Concurrencia Optimista):**
   - Cada `Producto` tiene un token de concurrencia opaco (`version` / `RowVersion` en SQL Server o `xmin`/contador en PostgreSQL).
   - `PUT /api/productos/{id}` exige `version` en el body.
   - Si Admin A y Admin B cargan la `version: V1`, Admin A guarda (`V1 -> V2`) y luego Admin B intenta guardar enviando `V1`, el backend DEBE rechazar con **`HTTP 409 Conflict`**:
     ```json
     {
       "codigo": "CONFLICTO_VERSION_PRODUCTO",
       "mensaje": "El producto fue editado por otro usuario. Recarga los datos antes de guardar."
     }
     ```
2. **Compras Simultáneas de Clientes (Concurrencia Atómica de Stock):**
   - **PROHIBIDO** bloquear una compra porque cambió la `version` administrativa del producto.
   - El descuento de inventario se ejecuta mediante sentencia atómica en base de datos dentro de la transacción de checkout:
     `UPDATE Productos SET Stock = Stock - @Cantidad WHERE Id = @ProductoId AND Activo = 1 AND Stock >= @Cantidad`
   - **Escenario A (Stock suficiente para ambos):** Stock = 100. Cliente A compra 50 y Cliente B compra 50 en el mismo milisegundo -> Ambos `UPDATE` afectan 1 fila -> Ambas compras se aprueban (`Stock` final = 0).
   - **Escenario B (Stock agotado):** Stock = 50. Cliente A compra 50 (`Stock` pasa a 0) y Cliente B intenta comprar 10 milisegundos después -> El `UPDATE` de Cliente B afecta 0 filas -> Se revierte la transacción de Cliente B y se responde **`HTTP 409 Conflict`**:
     ```json
     {
       "codigo": "STOCK_AGOTADO",
       "mensaje": "El producto 'X' ya no cuenta con stock suficiente o se ha agotado mientras realizabas tu compra.",
       "productoId": "uuid",
       "stockDisponible": 0
     }
     ```

---

## 5. Endpoints de E-Commerce (Nuevos sobre `/api`)

### 5.1 Autenticación de Clientes
- `POST /api/autenticacion/registro-cliente` (Público): Crea usuario con rol `Cliente` y devuelve `{ token, usuario }`.
  - Body: `{ "nombreCompleto": "", "correo": "", "telefono": "", "password": "" }`
- `POST /api/autenticacion/login` (Existente): Admite roles `Administrador`, `Repartidor` y ahora `Cliente`.

### 5.2 Catálogo Público
- `GET /api/categorias` (Público): Lista las categorías activas con conteo de productos.
- `GET /api/productos` (Público): Query params: `categoriaId`, `buscar`, `soloOfertas` (bool), `precioMin`, `precioMax`, `orden` (`relevancia|precio_asc|precio_desc|calificacion`), `pagina`, `tamanoPagina`. Devuelve `{ items, total, pagina, tamanoPagina }`.
- `GET /api/productos/{id}` (Público): Devuelve ficha completa + últimas reseñas.
- `POST /api/productos/{id}/resenas` (Rol `Cliente`): Permite calificar (1-5) y comentar si el cliente compró el producto.

### 5.3 Administración de Catálogo (Rol `Administrador`)
- `POST /api/productos` (Rol `Administrador`): Crea producto nuevo.
- `PUT /api/productos/{id}` (Rol `Administrador`): Edita producto validando `version` (retorna `409` ante conflicto).
- `PATCH /api/productos/{id}/stock` (Rol `Administrador`): Ajuste rápido de inventario.
- `DELETE /api/productos/{id}` (Rol `Administrador`): Desactivación lógica (`activo = false`).

### 5.4 Checkout y Órdenes (Rol `Cliente`)
- `POST /api/ordenes/checkout` (Rol `Cliente`): Procesa pago, descuenta stock atómicamente, crea la `Orden` y, si `tipoEntrega == "Domicilio"`, crea el `Pedido` logístico vinculado.
  - **Request Body (`Domicilio`):**
    ```json
    {
      "items": [
        { "productoId": "uuid", "cantidad": 2 }
      ],
      "metodoPago": {
        "tipo": "Tarjeta",
        "titular": "Juan Pérez",
        "ultimos4": "4242",
        "referenciaSimulada": "PAY-987654"
      },
      "tipoEntrega": "Domicilio",
      "datosRetiroTienda": null,
      "datosDomicilio": {
        "destinatarioNombre": "Juan Pérez",
        "destinatarioTelefono": "0991234567",
        "direccionTexto": "Av. Cevallos y Montalvo, Ambato",
        "latitud": -1.2416,
        "longitud": -78.6197,
        "fechaEntrega": "2026-11-15",
        "horaEntrega": "14:00:00",
        "observaciones": "Casa portón negro"
      }
    }
    ```
  - **Variante Request Body (`RetiroTienda`):**
    ```json
    {
      "datosRetiroTienda": {
        "cedulaQuienRetira": "1804567890",
        "nombreQuienRetira": "María López",
        "telefonoContacto": "0987654321",
        "fechaRetiro": "2026-11-15",
        "franjaHoraria": "10:00 - 12:00"
      },
      "datosDomicilio": null
    }
    ```
  - **Response `201 Created`:**
    ```json
    {
      "ordenId": "uuid",
      "numeroOrden": "ORD-2026-000145",
      "estadoOrden": "Pagada",
      "totalPagado": 29.98,
      "tipoEntrega": "Domicilio",
      "codigoRetiro": null,
      "pedidoLogisticoId": "uuid-del-pedido-creado",
      "estadoSeguimiento": "Pendiente"
    }
    ```
- `GET /api/ordenes/mias` (Rol `Cliente`): Lista compras del cliente con estado de pago, estado de entrega logística (enlazado al estado real del `Pedido` si es domicilio) y código de retiro si es en tienda.
- `GET /api/ordenes/retiros-tienda` (Rol `Administrador`): Lista órdenes de retiro en tienda para validar entrega en mostrador (`PATCH /api/ordenes/{id}/marcar-retirado`).

---

## 6. Regla de Oro de Algoritmos Propios (Sin APIs Externas de Ruteo)

- El cálculo de cobertura geográfica (Point-in-Polygon / Ray Casting), cálculo de distancias (Haversine / Euclidiana ponderada) y el **armado y optimización de rutas (CVRP / Vecino más cercano + inserción por ventana horaria y capacidad)** son **100% algoritmos propios implementados en el backend**.
- **PROHIBIDO** invocar APIs externas de pago por uso para matrices de distancia o ruteo (no Google Routes/Distance Matrix, no Mapbox Directions, no OSRM externo en el backend).