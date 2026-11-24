using Application.Exceptions;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Application.Common;

internal static class Extensiones
{
    /// <summary>Fecha local de hoy, para decidir qué pedidos son "de último momento".</summary>
    public static DateOnly Hoy(this TimeProvider reloj) => DateOnly.FromDateTime(reloj.GetLocalNow().DateTime);

    /// <summary>Guarda y traduce el choque de concurrencia en un error que el cliente puede reintentar.</summary>
    public static async Task GuardarAsync(this IAppDbContext db, CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictoException("Otra operación modificó los mismos datos. Intente nuevamente.");
        }
    }

    private const int ClaveBloqueoPlanificacion = 0x0DE1_1A55;

    /// <summary>
    /// Serializa, hasta el fin de la transacción actual, las operaciones que comprometen capacidad
    /// de entrega de una fecha (pg_advisory_xact_lock). Así dos compras simultáneas no reservan el
    /// mismo último cupo. En proveedores no PostgreSQL (pruebas en memoria) no hace nada.
    /// </summary>
    public static async Task BloquearPlanificacionAsync(this IAppDbContext db, DateOnly fecha, CancellationToken ct)
    {
        if (db is not DbContext contexto || contexto.Database.CurrentTransaction is null ||
            contexto.Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) != true)
            return;
        await contexto.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock({0}, {1})",
            [ClaveBloqueoPlanificacion, fecha.DayNumber], ct);
    }

    public static IQueryable<Ruta> ConParadas(this IQueryable<Ruta> rutas) =>
        rutas.Include(r => r.Paradas).ThenInclude(p => p.Pedido);

    /// <summary>Repartidores disponibles y sin otra ruta activa en la fecha.</summary>
    public static async Task<List<Repartidor>> RepartidoresLibresAsync(
        this IAppDbContext db, DateOnly fecha, CancellationToken ct)
    {
        var ocupados = await db.Rutas
            .Where(r => r.Fecha == fecha && (r.Estado == EstadoRuta.Planificada || r.Estado == EstadoRuta.EnCurso))
            .Select(r => r.RepartidorId)
            .ToListAsync(ct);

        return await db.Repartidores
            .Where(r => r.Estado == EstadoRepartidor.Disponible && !ocupados.Contains(r.Id))
            .OrderBy(r => r.Id)
            .ToListAsync(ct);
    }
}
