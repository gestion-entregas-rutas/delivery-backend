using Application.Common;
using Application.DTOs;
using Application.Exceptions;
using Application.Interfaces;
using Application.Mappings;
using Domain.Entities;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Application.Services;

public sealed class ZonaService(
    IAppDbContext db,
    IValidator<CrearZonaRequest> validadorCrear,
    IValidator<ActualizarZonaRequest> validadorActualizar) : IZonaService
{
    public async Task<ZonaDto> CrearAsync(CrearZonaRequest request, CancellationToken ct = default)
    {
        await validadorCrear.ValidateAndThrowAsync(request, ct);

        var zona = Zona.Crear(request.Nombre, request.Poligono.Select(p => p.ToGeoPoint()), request.MaxPedidosPorRuta);
        db.Zonas.Add(zona);
        await db.GuardarAsync(ct);
        return zona.ToDto();
    }

    public async Task<ZonaDto> ActualizarAsync(Guid id, ActualizarZonaRequest request, CancellationToken ct = default)
    {
        await validadorActualizar.ValidateAndThrowAsync(request, ct);

        var zona = await db.Zonas.FirstOrDefaultAsync(z => z.Id == id, ct)
                   ?? throw new NoEncontradoException(nameof(Zona), id);

        zona.Actualizar(request.Nombre, request.Poligono.Select(p => p.ToGeoPoint()), request.MaxPedidosPorRuta);
        if (request.Activa) zona.Activar(); else zona.Desactivar();

        await db.GuardarAsync(ct);
        return zona.ToDto();
    }

    public async Task<ZonaDto> ObtenerAsync(Guid id, CancellationToken ct = default) =>
        (await db.Zonas.AsNoTracking().FirstOrDefaultAsync(z => z.Id == id, ct)
         ?? throw new NoEncontradoException(nameof(Zona), id)).ToDto();

    public async Task<IReadOnlyList<ZonaDto>> ListarAsync(CancellationToken ct = default) =>
        (await db.Zonas.AsNoTracking().OrderBy(z => z.Nombre).ToListAsync(ct)).Select(z => z.ToDto()).ToList();
}
