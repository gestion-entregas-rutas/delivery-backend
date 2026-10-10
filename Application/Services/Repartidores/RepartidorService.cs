using Application.Common;
using Application.DTOs;
using Application.Exceptions;
using Application.Interfaces;
using Application.Mappings;
using Domain.Entities;
using Domain.Enums;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Application.Services;

public sealed class RepartidorService(
    IAppDbContext db,
    IValidator<CrearRepartidorRequest> validador) : IRepartidorService
{
    public async Task<RepartidorDto> CrearAsync(CrearRepartidorRequest request, CancellationToken ct = default)
    {
        await validador.ValidateAndThrowAsync(request, ct);

        var repartidor = Repartidor.Crear(request.Nombre, request.Telefono, request.CapacidadMaxima);
        db.Repartidores.Add(repartidor);
        await db.GuardarAsync(ct);
        return repartidor.ToDto();
    }

    public async Task<IReadOnlyList<RepartidorDto>> ListarAsync(bool soloDisponibles, CancellationToken ct = default)
    {
        var consulta = db.Repartidores.AsNoTracking().AsQueryable();
        if (soloDisponibles) consulta = consulta.Where(r => r.Estado == EstadoRepartidor.Disponible);

        return (await consulta.OrderBy(r => r.Nombre).ToListAsync(ct)).Select(r => r.ToDto()).ToList();
    }

    public async Task<RepartidorDto> CambiarDisponibilidadAsync(Guid id, bool disponible, CancellationToken ct = default)
    {
        var repartidor = await db.Repartidores.FirstOrDefaultAsync(r => r.Id == id, ct)
                         ?? throw new NoEncontradoException(nameof(Repartidor), id);

        if (disponible) repartidor.MarcarDisponible(); else repartidor.MarcarNoDisponible();

        await db.GuardarAsync(ct);
        return repartidor.ToDto();
    }

    public async Task<RutaDto?> ObtenerRutaAsignadaAsync(Guid id, DateOnly fecha, CancellationToken ct = default)
    {
        var ruta = await db.Rutas.AsNoTracking().ConParadas()
            .FirstOrDefaultAsync(r => r.RepartidorId == id && r.Fecha == fecha &&
                                      (r.Estado == EstadoRuta.Planificada || r.Estado == EstadoRuta.EnCurso), ct);

        return ruta?.ToDto();
    }
}
