// Prueba de ESTRÉS (escritura): clientes comprando en paralelo, mezcla Domicilio/RetiroTienda.
// El checkout reserva stock, así que CONSUME inventario real: usa SOLO una base de PRUEBAS.
// Uso: k6 run loadtests/02-checkout-estres.js
// Variables: -e CLIENTES=20 -e VUS=40 -e STOCK_MIN=500
import http from 'k6/http';
import { check, sleep, fail } from 'k6';
import { Counter, Rate, Trend } from 'k6/metrics';
import { BASE, auth, registrarClientes, listarProductos, checkoutBody } from './lib.js';

const CLIENTES = Number(__ENV.CLIENTES || 20); // más clientes = carga menos concentrada
const VUS = Number(__ENV.VUS || 40);
// Con VUS*2 usuarios y 1-3 unidades por compra se consumen cientos de unidades por
// segundo: un stock bajo se agota enseguida y todo pasa a ser 409.
const STOCK_MIN = Number(__ENV.STOCK_MIN || 500);
const ESPERADOS = [201, 409, 422];

const ok201 = new Counter('checkout_201');
const stock409 = new Counter('checkout_409_stock_agotado');
const cobertura422 = new Counter('checkout_422');
const otros = new Counter('checkout_inesperado');
const compraReal = new Rate('checkout_compra_real'); // proporción de compras 201
const compraMs = new Trend('checkout_compra_ms', true); // latencia SOLO de las 201

export const options = {
  setupTimeout: '5m',
  stages: [
    { duration: '20s', target: Math.ceil(VUS / 4) },
    { duration: '40s', target: VUS },
    { duration: '1m', target: VUS * 2 }, // pico de estrés
    { duration: '20s', target: 0 },
  ],
  thresholds: {
    checkout_inesperado: [
      'count==0', // solo se aceptan 201, 409 y 422
      // Si el script está roto, no seguir golpeando el servidor.
      { threshold: 'count<20', abortOnFail: true, delayAbortEval: '5s' },
    ],
    'http_req_duration{endpoint:checkout}': ['p(95)<2500'],
  },
  summaryTrendStats: ['avg', 'min', 'med', 'p(90)', 'p(95)', 'p(99)', 'max'],
};

// GUID válido para la cabecera Idempotency-Key.
function uuid() {
  return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, (c) => {
    const r = (Math.random() * 16) | 0;
    return (c === 'x' ? r : (r & 0x3) | 0x8).toString(16);
  });
}

function comprar(token, productoId, cantidad, tipo) {
  const params = auth(token);
  params.headers['Idempotency-Key'] = uuid();
  params.tags = { endpoint: 'checkout' };
  params.responseCallback = http.expectedStatuses(...ESPERADOS);
  return http.post(`${BASE}/api/ordenes/checkout`, checkoutBody(productoId, cantidad, tipo), params);
}

export function setup() {
  const tokens = registrarClientes(CLIENTES, 'estres');
  if (!tokens || tokens.length === 0 || tokens.some((t) => !t)) {
    fail('registrarClientes no devolvió tokens válidos. Revisa el registro y lib.js.');
  }
  const prods = listarProductos(50).filter((p) => p.activo && p.stock >= STOCK_MIN);
  if (prods.length === 0) {
    fail(`No hay productos activos con stock >= ${STOCK_MIN}. Repón inventario en la base de PRUEBAS.`);
  }

  // Compra de prueba: si ya falla aquí, el resto de la prueba no sirve.
  const r = comprar(tokens[0], prods[0].id, 1, 'Domicilio');
  if (!ESPERADOS.includes(r.status)) {
    fail(`El checkout de prueba respondió HTTP ${r.status}: ${String(r.body).slice(0, 300)}`);
  }
  return { tokens, productos: prods.map((p) => p.id) };
}

let muestras = 0; // por usuario virtual: limita cuántos errores se imprimen
let muestras422 = 0;

export default function (d) {
  const token = d.tokens[(__VU - 1) % d.tokens.length];
  const prod = d.productos[Math.floor(Math.random() * d.productos.length)];
  const tipo = Math.random() < 0.5 ? 'Domicilio' : 'RetiroTienda';
  const cantidad = 1 + Math.floor(Math.random() * 3);

  const r = comprar(token, prod, cantidad, tipo);

  if (r.status === 201) {
    ok201.add(1);
    compraReal.add(true);
    compraMs.add(r.timings.duration);
  } else {
    compraReal.add(false);
    if (r.status === 409) stock409.add(1);
    else if (r.status === 422) {
      cobertura422.add(1);
      if (muestras422++ < 2) console.warn(`422 (${tipo}): ${String(r.body).slice(0, 250)}`);
    }
    else {
      otros.add(1);
      if (muestras++ < 2) {
        console.error(`checkout inesperado: HTTP ${r.status} · ${String(r.body).slice(0, 200)}`);
      }
    }
  }

  check(r, { 'estado esperado (201/409/422)': (x) => ESPERADOS.includes(x.status) });
  sleep(Math.random() * 1.5);
}

// Recuperación: tras bajar la carga, el servidor debe responder con normalidad.
export function teardown() {
  const r = http.get(`${BASE}/api/categorias`, { tags: { endpoint: 'recuperacion' } });
  console.log(`Recuperación: HTTP ${r.status} en ${r.timings.duration.toFixed(0)} ms`);
}