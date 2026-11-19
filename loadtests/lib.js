import http from 'k6/http';
import { sleep } from 'k6';

export const BASE = __ENV.BASE_URL || 'http://localhost:5289';
export const JSON_HDR = { 'Content-Type': 'application/json' };
export const PASSWORD = 'PruebaCarga#2026';

// Punto DENTRO de una zona activa de la base que usa la API. Cada base tiene zonas distintas:
//   Supabase (6 zonas de Ambato): Ingahurco -> lat -1.23436, lon -78.61547 (valor por defecto)
//   PostgreSQL local (zona "Quito Norte"):    -e LAT=-0.16 -e LON=-78.49
export const COORD_COBERTURA = {
  lat: Number(__ENV.LAT || -1.23436),
  lon: Number(__ENV.LON || -78.61547),
};

export function manana() {
  const d = new Date(Date.now() + 24 * 3600 * 1000);
  return d.toISOString().slice(0, 10);
}

// Registra N clientes. El endpoint tiene rate limit de 10 req/min por IP,
// por eso se espera entre registros cuando hace falta.
export function registrarClientes(n, prefijo) {
  const tokens = [];
  const sello = Date.now();
  for (let i = 0; i < n; i++) {
    const body = JSON.stringify({
      nombreCompleto: `Carga ${prefijo} ${i}`,
      correo: `carga.${prefijo}.${sello}.${i}@prueba.local`,
      telefono: '0991234567',
      password: PASSWORD,
    });
    let r = http.post(`${BASE}/api/autenticacion/registro-cliente`, body, { headers: JSON_HDR });
    if (r.status === 429) {
      sleep(61);
      r = http.post(`${BASE}/api/autenticacion/registro-cliente`, body, { headers: JSON_HDR });
    }
    if (r.status !== 200) throw new Error(`registro fallo (${r.status}): ${r.body}`);
    tokens.push(r.json('accessToken'));
    if ((i + 1) % 8 === 0) sleep(61);
  }
  return tokens;
}

export function auth(token) {
  return { headers: { ...JSON_HDR, Authorization: `Bearer ${token}` } };
}

export function loginAdmin() {
  const u = __ENV.ADMIN_USER;
  const p = __ENV.ADMIN_PASS;
  if (!u || !p) return null;
  const r = http.post(`${BASE}/api/autenticacion/login`, JSON.stringify({ nombreUsuario: u, clave: p }), { headers: JSON_HDR });
  return r.status === 200 ? r.json('accessToken') : null;
}

export function listarProductos(tamano = 50) {
  const r = http.get(`${BASE}/api/productos?pagina=1&tamanoPagina=${tamano}`);
  return r.json('items');
}

export function checkoutBody(productoId, cantidad, tipo) {
  const base = {
    items: [{ productoId, cantidad }],
    metodoPago: { tipo: 'Simulado', titular: 'Carga K6', ultimos4: '4242', referenciaSimulada: null },
    tipoEntrega: tipo,
    datosRetiroTienda: null,
    datosDomicilio: null,
  };
  if (tipo === 'Domicilio') {
    base.datosDomicilio = {
      destinatarioNombre: 'Cliente Carga',
      destinatarioTelefono: '0991234567',
      direccionTexto: 'Av. Cevallos y Montalvo',
      latitud: COORD_COBERTURA.lat,
      longitud: COORD_COBERTURA.lon,
      fechaEntrega: manana(),
      horaEntrega: '10:00:00',
      observaciones: 'prueba de carga',
    };
  } else {
    base.datosRetiroTienda = {
      cedulaQuienRetira: '1800000000',
      nombreQuienRetira: 'Cliente Carga',
      telefonoContacto: '0991234567',
      fechaRetiro: manana(),
      franjaHoraria: '10:00-12:00',
    };
  }
  return JSON.stringify(base);
}
