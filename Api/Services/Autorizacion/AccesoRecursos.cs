using System.Security.Claims;
using Application.Exceptions;
using Application.Interfaces;
using Microsoft.EntityFrameworkCore;
namespace Api.Services;

public sealed class AccesoRecursos(IAppDbContext db)
{
    public async Task ExigirRuta(ClaimsPrincipal user, Guid id, CancellationToken ct)
    {
        if (user.IsInRole("Administrador")) return;
        var rep = Repartidor(user);
        if (!await db.Rutas.AnyAsync(x => x.Id == id && x.RepartidorId == rep, ct)) throw new AccesoDenegadoException();
    }
    public async Task ExigirPedido(ClaimsPrincipal user, Guid id, CancellationToken ct)
    {
        if (user.IsInRole("Administrador")) return;
        var rep = Repartidor(user);
        if (!await db.Pedidos.AnyAsync(p => p.Id == id && db.Rutas.Any(r => r.Id == p.RutaId && r.RepartidorId == rep), ct))
            throw new AccesoDenegadoException();
    }
    public static Guid Repartidor(ClaimsPrincipal user) =>
        user.IsInRole("Conductor") && Guid.TryParse(user.FindFirst("repartidor_id")?.Value, out var id)
            ? id : throw new AccesoDenegadoException();
}
