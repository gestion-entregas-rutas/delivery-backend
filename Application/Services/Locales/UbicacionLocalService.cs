using Application.Common;
using Application.DTOs;
using Application.Interfaces;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Application.Services;

/// <summary>
/// Ubicación del local guardada en el servidor: la comparten el panel (armado de rutas)
/// y la tienda (oferta de intervalos), en lugar de depender del navegador del administrador.
/// </summary>
public sealed class UbicacionLocalService(IAppDbContext db, TimeProvider reloj) : IUbicacionLocalService
{
    public async Task<UbicacionLocalDto?> ObtenerAsync(CancellationToken ct = default)
    {
        var local = await db.UbicacionLocal.AsNoTracking().FirstOrDefaultAsync(l => l.Id == UbicacionLocal.IdUnico, ct);
        return local is null ? null : ADto(local);
    }

    public async Task<UbicacionLocalDto> GuardarAsync(GuardarUbicacionLocalRequest request, CancellationToken ct = default)
    {
        var ahora = reloj.GetUtcNow();
        var local = await db.UbicacionLocal.FirstOrDefaultAsync(l => l.Id == UbicacionLocal.IdUnico, ct);
        if (local is null)
        {
            local = UbicacionLocal.Crear(request.Nombre, request.Direccion, request.Latitud, request.Longitud, ahora);
            db.UbicacionLocal.Add(local);
        }
        else
        {
            local.Actualizar(request.Nombre, request.Direccion, request.Latitud, request.Longitud, ahora);
        }
        await db.GuardarAsync(ct);
        return ADto(local);
    }

    private static UbicacionLocalDto ADto(UbicacionLocal l) => new(l.Nombre, l.Direccion, l.Latitud, l.Longitud, l.ActualizadoEn);
}
