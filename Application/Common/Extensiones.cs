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
