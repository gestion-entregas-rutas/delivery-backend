using Application.Common;
using Application.DTOs;
using Application.Exceptions;
using Application.Interfaces;
using Application.Mappings;
using Domain.Entities;
using Domain.Enums;
using Domain.Exceptions;
using Domain.Interfaces;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Application.Services;

public sealed class RutaService(
    IAppDbContext db,
    IPlanificadorRutas planificador,
    IValidator<ArmarRutasRequest> validador,
    INotificadorEventos notificador,
    TimeProvider reloj) : IRutaService
{
    public async Task<ArmadoRutasDto> ArmarRutasAsync(ArmarRutasRequest request, CancellationToken ct = default)
    {
        await validador.ValidateAndThrowAsync(request, ct);
        if (request.Fecha < reloj.Hoy())
            throw new DomainException("No se pueden armar rutas para una fecha pasada.");

        var zonas = await db.Zonas
            .Where(z => z.Activa && (request.ZonaId == null || z.Id == request.ZonaId))
            .OrderBy(z => z.Nombre)
            .ToListAsync(ct);

        if (request.ZonaId is { } zonaId && zonas.Count == 0)
            throw new NoEncontradoException(nameof(Zona), zonaId);

        var libres = await db.RepartidoresLibresAsync(request.Fecha, ct);
        var creadas = new List<Ruta>();
        var sinAsignar = 0;

        foreach (var zona in zonas)
        {
            var pendientes = await db.Pedidos
                .Where(p => p.FechaEntrega == request.Fecha && p.ZonaId == zona.Id &&
                            p.Estado == EstadoPedido.Pendiente)
                .OrderBy(p => p.HoraEntrega).ThenBy(p => p.Id)
                .ToListAsync(ct);

            if (pendientes.Count == 0) continue;

            var asignaciones = libres.Count == 0
                ? []
                : planificador.Planificar(zona, pendientes, libres);

            foreach (var asignacion in asignaciones.Where(a => a.PedidosEnOrden.Count > 0))
            {
                var ruta = Ruta.Crear(zona, asignacion.Repartidor, request.Fecha, reloj.GetUtcNow());
                foreach (var pedido in asignacion.PedidosEnOrden)
                    ruta.AgregarPedido(pedido);

                db.Rutas.Add(ruta);
                creadas.Add(ruta);
                libres.Remove(asignacion.Repartidor); // un repartidor, una ruta por armado
            }

            sinAsignar += pendientes.Count(p => p.Estado == EstadoPedido.Pendiente);
        }

        await db.GuardarAsync(ct);

        var dtos = creadas.Select(r => r.ToDto()).ToList();
        foreach (var dto in dtos)
            await notificador.RutaActualizadaAsync(dto, ct);

        return new ArmadoRutasDto(request.Fecha, dtos, sinAsignar);
    }

    public async Task<RutaDto> ObtenerAsync(Guid id, CancellationToken ct = default) =>
        (await db.Rutas.AsNoTracking().ConParadas().FirstOrDefaultAsync(r => r.Id == id, ct)
         ?? throw new NoEncontradoException(nameof(Ruta), id)).ToDto();

    public async Task<IReadOnlyList<RutaDto>> ListarAsync(DateOnly fecha, Guid? zonaId, CancellationToken ct = default)
    {
        var rutas = await db.Rutas.AsNoTracking().ConParadas()
            .Where(r => r.Fecha == fecha && (zonaId == null || r.ZonaId == zonaId))
            .OrderBy(r => r.ZonaId).ThenBy(r => r.CreadaEn)
            .ToListAsync(ct);

        return rutas.Select(r => r.ToDto()).ToList();
    }

    public async Task<RutaDto> AgregarPedidoAsync(Guid rutaId, Guid pedidoId, int? posicion, CancellationToken ct = default)
    {
        var ruta = await CargarAsync(rutaId, ct);
        var pedido = await db.Pedidos.FirstOrDefaultAsync(p => p.Id == pedidoId, ct)
                     ?? throw new NoEncontradoException(nameof(Pedido), pedidoId);

        ruta.AgregarPedido(pedido, posicion);
        return await GuardarYNotificarAsync(ruta, ct);
    }

    public async Task<RutaDto> QuitarPedidoAsync(Guid rutaId, Guid pedidoId, CancellationToken ct = default)
    {
        var ruta = await CargarAsync(rutaId, ct);
        var pedido = ruta.Paradas.FirstOrDefault(p => p.PedidoId == pedidoId)?.Pedido;

        ruta.QuitarPedido(pedidoId);
        var dto = await GuardarYNotificarAsync(ruta, ct);

        if (pedido is not null)
            await notificador.PedidoActualizadoAsync(pedido.ToDto(), ct);
        return dto;
    }

    public async Task<RutaDto> IniciarAsync(Guid rutaId, CancellationToken ct = default)
    {
        var ruta = await CargarAsync(rutaId, ct);
        var repartidor = await db.Repartidores.FirstAsync(r => r.Id == ruta.RepartidorId, ct);

        ruta.Iniciar(repartidor, reloj.GetUtcNow());
        return await GuardarYNotificarAsync(ruta, ct);
    }

    public async Task<RutaDto> CancelarAsync(Guid rutaId, CancellationToken ct = default)
    {
        var ruta = await CargarAsync(rutaId, ct);
        var pedidos = ruta.Paradas.Select(p => p.Pedido).ToList();

        ruta.Cancelar();
        var dto = await GuardarYNotificarAsync(ruta, ct);

        foreach (var pedido in pedidos)
            await notificador.PedidoActualizadoAsync(pedido.ToDto(), ct);
        return dto;
    }

    private async Task<Ruta> CargarAsync(Guid id, CancellationToken ct) =>
        await db.Rutas.ConParadas().FirstOrDefaultAsync(r => r.Id == id, ct)
        ?? throw new NoEncontradoException(nameof(Ruta), id);

    private async Task<RutaDto> GuardarYNotificarAsync(Ruta ruta, CancellationToken ct)
    {
        await db.GuardarAsync(ct);
        var dto = ruta.ToDto();
        await notificador.RutaActualizadaAsync(dto, ct);
        return dto;
    }
}
