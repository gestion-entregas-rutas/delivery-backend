# Pruebas de carga (k6)

Instalar: `winget install k6`. Arrancar la API (`dotnet run --project Api`, puerto 5289).

| Script | Tipo | Qué mide | Modifica datos |
|---|---|---|---|
| `01-lectura.js` | Carga | Catálogo, detalle y búsqueda con 50 usuarios virtuales | No |
| `02-checkout-estres.js` | Estrés | Checkout concurrente (Domicilio y RetiroTienda), pico al doble | Sí: descuenta stock, crea órdenes y pedidos |
| `03-concurrencia-stock.js` | Concurrencia | Escenarios B y C de AGENTS.md (201/409, stock exacto) | Sí: ajusta y restaura el stock de un producto |

```powershell
k6 run loadtests/01-lectura.js
k6 run loadtests/02-checkout-estres.js
k6 run -e ADMIN_USER=admin -e ADMIN_PASS=<clave> loadtests/03-concurrencia-stock.js

# Volumen opcional antes del estrés (solo Development):
curl -X POST http://localhost:5289/api/testing/poblar-escenario-alto-volumen

# Otra URL o más carga:
k6 run -e BASE_URL=http://otra:5289 -e VUS=100 loadtests/01-lectura.js
```

Notas:
- `registro-cliente` y `login` tienen límite de 10 req/min por IP. Los scripts registran pocos clientes y esperan 61 s si hace falta, así que el setup puede tardar cerca de un minuto.
- El estrés necesita productos con stock >= 50. Tras la prueba el inventario queda reducido; restáuralo con `PATCH /api/productos/{id}/stock`.
- `409 STOCK_AGOTADO` y `422` (sin cobertura) son respuestas válidas y se cuentan aparte. Cualquier otro código rompe el umbral `checkout_inesperado`.
- Contra Supabase gratuito, los errores de conexión pueden venir del límite del pooler y no del código.
- Cada ejecución del estrés crea cientos de órdenes y pedidos en la base. No lo corras contra la base de la demo final.
