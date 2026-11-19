// Pruebas B y C de AGENTS.md contra la API real (requiere usuario administrador).
//  B: stock=100, 2 clientes comprando 50 en paralelo -> ambos 201, stock final 0.
//  C: stock=50,  2 clientes comprando 40 en paralelo -> un 201, un 409 STOCK_AGOTADO, stock final 10.
// Uso: k6 run -e ADMIN_USER=admin -e ADMIN_PASS=clave loadtests/03-concurrencia-stock.js
import http from 'k6/http';
import { check, fail } from 'k6';
import { BASE, auth, loginAdmin, registrarClientes, listarProductos, checkoutBody } from './lib.js';

export const options = { setupTimeout: '3m', vus: 1, iterations: 1 };

function ajustarStock(adminToken, id, stock) {
  const r = http.patch(`${BASE}/api/productos/${id}/stock`, JSON.stringify({ stock }), auth(adminToken));
  if (r.status !== 200) fail(`No se pudo ajustar stock (${r.status}): ${r.body}`);
}

function stockActual(id) {
  return http.get(`${BASE}/api/productos/${id}`).json('stock');
}

function comprarEnParalelo(tokens, productoId, cantidad) {
  const reqs = tokens.map((t) => {
    const p = auth(t);
    p.responseCallback = http.expectedStatuses(201, 409);
    return ['POST', `${BASE}/api/ordenes/checkout`, checkoutBody(productoId, cantidad, 'RetiroTienda'), p];
  });
  return http.batch(reqs);
}

export function setup() {
  const admin = loginAdmin();
  if (!admin) fail('Falta ADMIN_USER/ADMIN_PASS o el login falló.');
  const tokens = registrarClientes(2, 'stock');
  const producto = listarProductos(50).find((p) => p.activo);
  return { admin, tokens, productoId: producto.id };
}

export default function (d) {
  // Escenario B
  ajustarStock(d.admin, d.productoId, 100);
  let res = comprarEnParalelo(d.tokens, d.productoId, 50);
  check(res, { 'B: ambos 201': (rs) => rs.every((r) => r.status === 201) });
  check(null, { 'B: stock final 0': () => stockActual(d.productoId) === 0 });

  // Escenario C
  ajustarStock(d.admin, d.productoId, 50);
  res = comprarEnParalelo(d.tokens, d.productoId, 40);
  const estados = res.map((r) => r.status).sort();
  check(estados, { 'C: un 201 y un 409': (x) => x[0] === 201 && x[1] === 409 });
  check(res, { 'C: el 409 trae STOCK_AGOTADO': (rs) => rs.filter((r) => r.status === 409).every((r) => r.body.includes('STOCK_AGOTADO')) });
  check(null, { 'C: stock final 10 (nunca negativo)': () => stockActual(d.productoId) === 10 });

  // Restaurar inventario
  ajustarStock(d.admin, d.productoId, 100);
}
