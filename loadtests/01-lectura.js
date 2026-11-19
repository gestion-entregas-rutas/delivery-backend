// Prueba de CARGA (lectura): catálogo y detalle de productos. No modifica datos.
// Objetivo: 100 usuarios virtuales y 5000 peticiones en 1 minuto (~83 peticiones/s).
// Cada iteración lanza 4 peticiones en paralelo (http.batch): 1250 iteraciones por
// minuto dan 5000 peticiones.
//
// MODOS (variable MODO):
//   usuarios (por defecto): 100 usuarios reales navegando, cada uno con una pausa
//                           entre acciones. Es lo que pide el objetivo.
//   tasa:                   ritmo fijo de llegada, sin importar cuánto tarden las
//                           respuestas. Sirve para buscar el LÍMITE del servidor.
//
// Uso:  k6 run loadtests/01-lectura.js
//       k6 run -e MODO=tasa loadtests/01-lectura.js
// Variables: -e VUS=100  -e PETICIONES_MIN=5000  -e DURACION=1m
//            -e BUSQUEDAS=cable,cargador,mouse
//
// Consejo: córrelo desde OTRO equipo, no desde el del servidor; si no, compiten
// por CPU y no hay latencia de red real.
import http from 'k6/http';
import { check, sleep, fail } from 'k6';
import { BASE } from './lib.js';

const VUS = Number(__ENV.VUS || 100);
const PETICIONES_MIN = Number(__ENV.PETICIONES_MIN || 5000);
const DURACION = __ENV.DURACION || '1m';
const MODO = __ENV.MODO || 'usuarios';
const PETICIONES_POR_ITERACION = 4;
const ITERACIONES_MIN = Math.ceil(PETICIONES_MIN / PETICIONES_POR_ITERACION);
// En modo "usuarios", cada uno hace una iteración cada PERIODO segundos
// (100 usuarios * 60 s / 1250 iteraciones = 4,8 s).
const PERIODO = (60 * VUS) / ITERACIONES_MIN;
const BUSQUEDAS = (__ENV.BUSQUEDAS || 'cable,cargador,audifonos,mouse,teclado,funda')
  .split(',')
  .map((t) => t.trim())
  .filter(Boolean);

// "1m" -> 60, "90s" -> 90, "2h" -> 7200
function segundos(texto) {
  const m = /^(\d+)\s*([smh])$/.exec(texto);
  if (!m) return 60;
  return Number(m[1]) * { s: 1, m: 60, h: 3600 }[m[2]];
}

const escenarios = {
  usuarios: {
    executor: 'ramping-vus',
    startVUs: 0,
    stages: [
      { duration: '10s', target: VUS }, // sube gradualmente
      { duration: DURACION, target: VUS }, // se mantiene
      { duration: '5s', target: 0 },
    ],
    gracefulRampDown: '10s',
  },
  tasa: {
    executor: 'constant-arrival-rate', // fija la tasa de llegada
    rate: ITERACIONES_MIN,
    timeUnit: '1m',
    duration: DURACION,
    preAllocatedVUs: VUS,
    maxVUs: VUS, // tope de usuarios virtuales
  },
};

if (!escenarios[MODO]) {
  throw new Error(`MODO desconocido: "${MODO}". Usa: usuarios o tasa.`);
}

const umbrales = {
  http_req_failed: [
    'rate<0.01',
    // Corte de emergencia: si falla más de la mitad, no seguir golpeando.
    { threshold: 'rate<0.5', abortOnFail: true, delayAbortEval: '10s' },
  ],
  http_req_duration: ['p(95)<800', 'p(99)<1500'],
  // Por endpoint: así se ve cuál es el lento.
  'http_req_duration{endpoint:categorias}': ['p(95)<800'],
  'http_req_duration{endpoint:productos}': ['p(95)<800'],
  'http_req_duration{endpoint:detalle}': ['p(95)<800'],
  'http_req_duration{endpoint:busqueda}': ['p(95)<800'],
  checks: ['rate>0.99'], // respuestas válidas, no solo "200"
};
if (MODO === 'tasa') {
  // Si aparecen, los usuarios no alcanzaron la tasa pedida.
  umbrales.dropped_iterations = ['count==0'];
} else {
  // Se llegó al volumen pedido (con 10 % de margen).
  umbrales.http_reqs = [
    `count>=${Math.floor((PETICIONES_MIN * 0.9 * segundos(DURACION)) / 60)}`,
  ];
}

export const options = {
  scenarios: { carga_lectura: escenarios[MODO] },
  thresholds: umbrales,
  summaryTrendStats: ['avg', 'min', 'med', 'p(90)', 'p(95)', 'p(99)', 'max'],
};

function jsonSeguro(r) {
  try {
    return r.json();
  } catch (_) {
    return null;
  }
}

function al_azar(lista) {
  return lista[Math.floor(Math.random() * lista.length)];
}

export function setup() {
  // Varias páginas: más productos distintos, menos efecto de caché.
  const ids = [];
  for (let pagina = 1; pagina <= 3; pagina++) {
    const r = http.get(`${BASE}/api/productos?pagina=${pagina}&tamanoPagina=50`, {
      tags: { endpoint: 'setup' },
    });
    if (r.status !== 200) {
      fail(`No se pudo leer el catálogo (HTTP ${r.status}). ¿Está el backend arriba en ${BASE}?`);
    }
    const items = (jsonSeguro(r) || {}).items || [];
    items.forEach((p) => ids.push(p.id));
    if (items.length < 50) break; // ya no hay más páginas
  }
  if (ids.length === 0) fail('El catálogo está vacío: no hay productos para probar.');
  return { productos: ids };
}

export default function (d) {
  const inicio = Date.now();
  const id = al_azar(d.productos);
  const termino = encodeURIComponent(al_azar(BUSQUEDAS));
  const ofertas = Math.random() < 0.5;
  const pagina = 1 + Math.floor(Math.random() * 3);

  const [categorias, productos, detalle, busqueda] = http.batch([
    ['GET', `${BASE}/api/categorias`, null, { tags: { endpoint: 'categorias' } }],
    ['GET', `${BASE}/api/productos?pagina=${pagina}&tamanoPagina=20`, null, { tags: { endpoint: 'productos' } }],
    ['GET', `${BASE}/api/productos/${id}`, null, { tags: { endpoint: 'detalle' } }],
    ['GET', `${BASE}/api/productos?buscar=${termino}&soloOfertas=${ofertas}`, null, { tags: { endpoint: 'busqueda' } }],
  ]);

  check(categorias, {
    'categorias 200': (r) => r.status === 200,
    'categorias JSON válido': (r) => jsonSeguro(r) !== null,
  });
  check(productos, {
    'productos 200': (r) => r.status === 200,
    'productos trae lista': (r) => Array.isArray((jsonSeguro(r) || {}).items),
  });
  check(detalle, {
    'detalle 200': (r) => r.status === 200,
    'detalle JSON válido': (r) => jsonSeguro(r) !== null,
  });
  check(busqueda, {
    'busqueda 200': (r) => r.status === 200,
    'busqueda trae lista': (r) => Array.isArray((jsonSeguro(r) || {}).items),
  });

  // Modo "usuarios": cada usuario "piensa" hasta completar su periodo, con un
  // poco de variación para que no se sincronicen todos.
  if (MODO === 'usuarios') {
    const gastado = (Date.now() - inicio) / 1000;
    sleep(Math.max(0.1, PERIODO * (0.9 + Math.random() * 0.2) - gastado));
  }
}