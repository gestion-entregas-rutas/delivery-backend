using Application.Common;
using Application.DTOs;
using Application.Exceptions;
using Application.Interfaces;
using Application.Mappings;
using Domain.Entities;
using Domain.Exceptions;
using Domain.ValueObjects;
using Domain.Services;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Application.Services;

public sealed class PedidoService(
    IAppDbContext db,
    IValidator<RegistrarPedidoRequest> validador,
    IValidator<ActualizarPedidoRequest> validadorEdicion,
    IAsignadorUltimoMomento asignador,
    INotificadorEventos notificador,
    TimeProvider reloj,
    ReglasPlanificacion reglas,
    IDisponibilidadEntregas disponibilidad) : IPedidoService
{
    public async Task<RegistroPedidoDto> RegistrarAsync(RegistrarPedidoRequest request, CancellationToken ct = default)
    {
        await validador.ValidateAndThrowAsync(request, ct);

        var ubicacion = new GeoPoint(request.Latitud, request.Longitud);
        var zona = await ResolverZonaAsync(request.ZonaId, ubicacion, ct);
        var hoy = reloj.Hoy();

        var pedido = Pedido.Registrar(
            request.ClienteNombre, request.ClienteTelefono, request.Direccion, ubicacion, zona,
            request.FechaEntrega, request.HoraEntrega, hoy, reloj.GetUtcNow(), request.Detalles.ToDomain());

        ValidarFechaCompromiso(request.FechaEntrega, request.CompromisoInicioEn, request.CompromisoFinEn);
        pedido.ConfigurarCompromiso(request.CompromisoInicioEn, request.CompromisoFinEn, request.TiempoServicioMinutos);

        // Con verificación: mismo criterio que el checkout (rejilla ofrecida, capacidad real y
        // bloqueo por fecha) para no prometer el mismo cupo a dos pedidos simultáneos.
        IDbContextTransaction? tx = null;
        try
        {
            if (request.VerificarDisponibilidad && request.CompromisoInicioEn is { } inicio && request.CompromisoFinEn is { } fin)
            {
                if (db is DbContext contexto && contexto.Database.IsRelational())
                    tx = await contexto.Database.BeginTransactionAsync(ct);
                await db.BloquearPlanificacionAsync(request.FechaEntrega, ct);
                await disponibilidad.VerificarAsync(request.FechaEntrega, ubicacion, inicio, fin, ct);
            }
            db.Pedidos.Add(pedido);
            await db.GuardarAsync(ct);
            if (tx is not null) await tx.CommitAsync(ct);
        }
        finally
        {
            if (tx is not null) await tx.DisposeAsync();
        }

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
        var desplazamiento = (long)(pagina - 1) * tamano;
        if (desplazamiento > int.MaxValue) throw new DomainException("La página solicitada supera el rango permitido.");

        var consulta = db.Pedidos.AsNoTracking().Filtrar(filtro);

        var total = await consulta.CountAsync(ct);
        var items = await consulta
            .OrderBy(p => p.FechaEntrega).ThenBy(p => p.HoraEntrega).ThenBy(p => p.Id)
            .Skip((int)desplazamiento).Take(tamano)
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

    public async Task<PedidoDto> ActualizarAsync(Guid id, ActualizarPedidoRequest r, CancellationToken ct = default)
    {
        await validadorEdicion.ValidateAndThrowAsync(r, ct);
        var pedido = await db.Pedidos.FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new NoEncontradoException(nameof(Pedido), id);
        if (pedido.Version != r.Version) throw new ConflictoException("El pedido cambió. Recargue antes de editar.");
        var punto = new GeoPoint(r.Latitud, r.Longitud);
        var zona = await ResolverZonaAsync(r.ZonaId, punto, ct);
        var anterior = pedido.FechaEntrega;
        // Un cliente anterior que no envía campos nuevos conserva el compromiso existente.
        var inicio = r.CompromisoInicioEn ?? pedido.CompromisoInicioEn;
        var fin = r.CompromisoFinEn ?? pedido.CompromisoFinEn;
        ValidarFechaCompromiso(r.FechaEntrega, inicio, fin);
        pedido.ConfigurarCompromiso(inicio, fin, r.TiempoServicioMinutos ?? pedido.TiempoServicioMinutos);
        pedido.Actualizar(r.ClienteNombre, r.ClienteTelefono, r.Direccion, punto, zona,
            r.FechaEntrega, r.HoraEntrega, reloj.Hoy(), r.Detalles.ToDomain());
        await db.GuardarAsync(ct);
        var dto = pedido.ToDto();
        await notificador.PedidoReprogramadoAsync(dto, anterior, ct);
        return dto;
    }

    private void ValidarFechaCompromiso(DateOnly fecha, DateTimeOffset? inicio, DateTimeOffset? fin)
    {
        var zona = TimeZoneInfo.FindSystemTimeZoneById(reglas.ZonaHoraria);
        foreach (var instante in new[] { inicio, fin }.OfType<DateTimeOffset>())
            if (DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instante, zona).DateTime) != fecha)
                throw new DomainException("El intervalo comprometido debe pertenecer a la fecha de entrega en la zona horaria del negocio.");
    }
}
