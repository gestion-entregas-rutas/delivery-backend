using Application.Common;
using Application.DTOs;
using Application.Exceptions;
using Application.Interfaces;
using Application.Mappings;
using Domain.Entities;
using Domain.Exceptions;
using Domain.ValueObjects;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Application.Services;

public sealed class PedidoService(
    IAppDbContext db,
    IValidator<RegistrarPedidoRequest> validador,
    IAsignadorUltimoMomento asignador,
    INotificadorEventos notificador,
    TimeProvider reloj) : IPedidoService
{
    public async Task<RegistroPedidoDto> RegistrarAsync(RegistrarPedidoRequest request, CancellationToken ct = default)
    {
        await validador.ValidateAndThrowAsync(request, ct);

        var ubicacion = new GeoPoint(request.Latitud, request.Longitud);
        var zona = await ResolverZonaAsync(request.ZonaId, ubicacion, ct);
        var hoy = reloj.Hoy();

        var pedido = Pedido.Registrar(
            request.ClienteNombre, request.ClienteTelefono, request.Direccion, ubicacion, zona,
            request.FechaEntrega, request.HoraEntrega, hoy, reloj.GetUtcNow());

        db.Pedidos.Add(pedido);
        await db.GuardarAsync(ct);

        ResultadoAsignacionDto? asignacion = null;
        if (pedido.EsDeUltimoMomento(hoy))
        {
            try
            {
                asignacion = await asignador.AsignarAsync(pedido.Id, ct);
            }
            catch (ConflictoException)
            {
                asignacion = new ResultadoAsignacionDto(TipoAsignacion.EnEspera, null);
            }

            pedido = await db.Pedidos.AsNoTracking().FirstAsync(p => p.Id == pedido.Id, ct);
        }

        var dto = pedido.ToDto();
        if (asignacion is null || asignacion.Tipo == TipoAsignacion.EnEspera)
            await notificador.PedidoActualizadoAsync(dto, ct);

        return new RegistroPedidoDto(dto, asignacion);
    }

    public async Task<PedidoDto> ObtenerAsync(Guid id, CancellationToken ct = default) =>
        (await db.Pedidos.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct)
         ?? throw new NoEncontradoException(nameof(Pedido), id)).ToDto();

    public async Task<PaginaDto<PedidoDto>> ListarAsync(FiltroPedidos filtro, CancellationToken ct = default)
    {
        var pagina = Math.Max(1, filtro.Pagina);
        var tamano = Math.Clamp(filtro.Tamano, 1, 200);

        var consulta = db.Pedidos.AsNoTracking().AsQueryable();
        if (filtro.Fecha is { } fecha) consulta = consulta.Where(p => p.FechaEntrega == fecha);
        if (filtro.ZonaId is { } zonaId) consulta = consulta.Where(p => p.ZonaId == zonaId);
        if (filtro.Estado is { } estado) consulta = consulta.Where(p => p.Estado == estado);

        var total = await consulta.CountAsync(ct);
        var items = await consulta
            .OrderBy(p => p.FechaEntrega).ThenBy(p => p.HoraEntrega).ThenBy(p => p.Id)
            .Skip((pagina - 1) * tamano).Take(tamano)
            .ToListAsync(ct);

        return new PaginaDto<PedidoDto>(items.Select(p => p.ToDto()).ToList(), total, pagina, tamano);
    }

    public async Task<PedidoDto> CancelarAsync(Guid id, CancellationToken ct = default)
    {
        var pedido = await db.Pedidos.FirstOrDefaultAsync(p => p.Id == id, ct)
                     ?? throw new NoEncontradoException(nameof(Pedido), id);

        pedido.Cancelar();
        await db.GuardarAsync(ct);

        var dto = pedido.ToDto();
        await notificador.PedidoActualizadoAsync(dto, ct);
        return dto;
    }

    private async Task<Zona> ResolverZonaAsync(Guid? zonaId, GeoPoint ubicacion, CancellationToken ct)
    {
        if (zonaId is { } id)
            return await db.Zonas.FirstOrDefaultAsync(z => z.Id == id, ct)
                   ?? throw new NoEncontradoException(nameof(Zona), id);

        var zonas = await db.Zonas.Where(z => z.Activa).ToListAsync(ct);
        return zonas.FirstOrDefault(z => z.Contiene(ubicacion))
               ?? throw new DomainException("La dirección no pertenece a ninguna zona de reparto activa.");
    }
}
