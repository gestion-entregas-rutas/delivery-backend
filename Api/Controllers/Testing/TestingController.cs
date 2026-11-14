using Domain.Entities;
using Domain.Enums;
using Domain.ValueObjects;
using Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers;

[ApiController]
[Route("api/testing")]
public sealed class TestingController(AppDbContext db, IWebHostEnvironment env, TimeProvider reloj) : ControllerBase
{
    [HttpPost("poblar-escenario-alto-volumen")]
    public async Task<ActionResult<object>> PoblarEscenarioAltoVolumen(CancellationToken ct)
    {
        if (!env.IsDevelopment())
            return NotFound();

        var hoy = DateOnly.FromDateTime(reloj.GetLocalNow().DateTime);
        var ahora = reloj.GetUtcNow();
        var random = new Random(20261114);

        // Zonas: asegurar varias zonas activas
        var zonas = await db.Zonas.Where(z => z.Activa).ToListAsync(ct);
        var zonasObjetivo = new (string Nombre, GeoPoint[] Poly)[]
        {
            ("Centro Alto Volumen", Cuadrado(-0.18, -78.50, 0.025)),
            ("Norte Alto Volumen", Cuadrado(-0.12, -78.50, 0.025)),
            ("Sur Alto Volumen", Cuadrado(-0.24, -78.50, 0.025)),
            ("Valle Alto Volumen", Cuadrado(-0.30, -78.45, 0.025)),
        };
        foreach (var z in zonasObjetivo)
        {
            if (!zonas.Any(x => x.Nombre == z.Nombre))
            {
                var nueva = Zona.Crear(z.Nombre, z.Poly, 20);
                db.Zonas.Add(nueva);
                zonas.Add(nueva);
            }
        }
        await db.SaveChangesAsync(ct);

        // Repartidores: garantizar 20+
        var totalRepartidores = await db.Repartidores.CountAsync(ct);
        var faltan = Math.Max(0, 20 - totalRepartidores);
        for (int i = 0; i < faltan; i++)
        {
            db.Repartidores.Add(Repartidor.Crear($"Repartidor Volumen {totalRepartidores + i + 1}", $"09{random.Next(10000000, 99999999)}", 20));
        }
        await db.SaveChangesAsync(ct);

        // Pedidos: cientos, programados y del día, dentro de zonas activas
        var zonasActivas = await db.Zonas.Where(z => z.Activa).ToListAsync(ct);
        int creados = 0;
        for (int i = 0; i < 300; i++)
        {
            var zona = zonasActivas[i % zonasActivas.Count];
            var punto = PuntoDentro(zona, random);
            var esHoy = i % 2 == 0; // mezcla día y programados
            var fecha = esHoy ? hoy : hoy.AddDays(1 + (i % 5));
            var hora = new TimeOnly(8 + (i % 10), (i % 4) * 15);
            var pedido = Pedido.Registrar(
                clienteNombre: $"Cliente Volumen {i + 1}",
                clienteTelefono: $"098{random.Next(1000000, 9999999)}",
                direccion: $"Dirección generada {i + 1}, zona {zona.Nombre}",
                ubicacion: punto,
                zona: zona,
                fechaEntrega: fecha,
                horaEntrega: hora,
                hoy: hoy,
                ahora: ahora,
                detalles: null);
            db.Pedidos.Add(pedido);
            creados++;
        }
        await db.SaveChangesAsync(ct);

        var repartidoresCount = await db.Repartidores.CountAsync(ct);
        var pendientesHoy = await db.Pedidos.CountAsync(p => p.FechaEntrega == hoy && p.Estado == EstadoPedido.Pendiente, ct);
        var pendientesProgramados = await db.Pedidos.CountAsync(p => p.FechaEntrega > hoy && p.Estado == EstadoPedido.Pendiente, ct);

        return Ok(new
        {
            mensaje = "Escenario de alto volumen generado.",
            zonasActivas = zonasActivas.Count,
            repartidores = repartidoresCount,
            pedidosCreados = creados,
            pedidosPendientesHoy = pendientesHoy,
            pedidosPendientesProgramados = pendientesProgramados
        });
    }

    private static GeoPoint[] Cuadrado(double latCentro, double lonCentro, double delta) =>
    [
        new(latCentro - delta, lonCentro - delta),
        new(latCentro - delta, lonCentro + delta),
        new(latCentro + delta, lonCentro + delta),
        new(latCentro + delta, lonCentro - delta),
    ];

    private static GeoPoint PuntoDentro(Zona zona, Random random)
    {
        // Generar dentro del bounding box y validar con ray casting (Contiene)
        var verts = zona.Poligono;
        var minLat = verts.Min(v => v.Latitud); var maxLat = verts.Max(v => v.Latitud);
        var minLon = verts.Min(v => v.Longitud); var maxLon = verts.Max(v => v.Longitud);
        for (int i = 0; i < 50; i++)
        {
            var lat = minLat + (maxLat - minLat) * random.NextDouble();
            var lon = minLon + (maxLon - minLon) * random.NextDouble();
            var p = new GeoPoint(lat, lon);
            if (zona.Contiene(p)) return p;
        }
        // fallback: centroide aproximado
        return new GeoPoint(verts.Average(v => v.Latitud), verts.Average(v => v.Longitud));
    }
}
