using Application.Common;
using Application.DTOs;
using Application.Exceptions;
using Application.Interfaces;
using Application.Mappings;
using Domain.Entities;
using Domain.Enums;
using Domain.Exceptions;
using Domain.ValueObjects;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
namespace Application.Services;

public sealed class UbicacionService(IAppDbContext db, INotificadorEventos notificador, TimeProvider reloj,
    IValidator<RegistrarUbicacionRequest> validador) : IUbicacionService
{
    public async Task<RegistroUbicacionDto> RegistrarAsync(Guid repartidorId, RegistrarUbicacionRequest r, CancellationToken ct)
    {
        await validador.ValidateAndThrowAsync(r, ct);
        var ruta = await db.Rutas.AsNoTracking().SingleOrDefaultAsync(x => x.Id == r.RutaId, ct)
            ?? throw new NoEncontradoException("Ruta", r.RutaId);
        if (ruta.RepartidorId != repartidorId) throw new AccesoDenegadoException();
        if (ruta.Estado != EstadoRuta.EnCurso) throw new DomainException("Solo se publica GPS durante una ruta en curso.");
        for (var intento = 0; intento < 3; intento++)
        {
            var u = await db.Ubicaciones.SingleOrDefaultAsync(x => x.RepartidorId == repartidorId, ct);
            if (u is null) { u = UltimaUbicacionRepartidor.Crear(repartidorId); db.Ubicaciones.Add(u); }
            var actualizada = u.Actualizar(r.RutaId, new GeoPoint(r.Latitud, r.Longitud), r.PrecisionMetros, r.CapturadaEn, reloj.GetUtcNow());
            if (actualizada)
            {
                try { await db.GuardarAsync(ct); }
                catch (ConflictoException) when (intento < 2) { db.LimpiarCambios(); continue; }
            }
            var dto = u.ToDto(reloj.GetUtcNow());
            if (actualizada) await notificador.UbicacionActualizadaAsync(dto, ruta.Fecha, ct);
            return new(actualizada, dto);
        }
        throw new ConflictoException("No se pudo actualizar la posición. Consulte la última ubicación.");
    }
    public async Task<UbicacionRepartidorDto> ObtenerAsync(Guid repartidorId, CancellationToken ct) =>
        (await db.Ubicaciones.AsNoTracking().SingleOrDefaultAsync(x => x.RepartidorId == repartidorId, ct)
            ?? throw new NoEncontradoException("Ubicación de repartidor", repartidorId)).ToDto(reloj.GetUtcNow());

    public async Task<IReadOnlyList<UbicacionRepartidorDto>> ListarAsync(DateOnly fecha, Guid? zonaId, CancellationToken ct)
    {
        var lista = await db.Ubicaciones.AsNoTracking().Where(u => db.Rutas.Any(r => r.Id == u.RutaId &&
            r.Fecha == fecha && (zonaId == null || r.ZonaId == zonaId))).OrderBy(u => u.RepartidorId).ToListAsync(ct);
        return lista.Select(u => u.ToDto(reloj.GetUtcNow())).ToList();
    }
}
