using Application.Common;
using Application.DTOs;
using Application.Exceptions;
using Application.Interfaces;
using Application.Mappings;
using Domain.Entities;
using Domain.Enums;
using Domain.Exceptions;
using Domain.Interfaces;
using Domain.Services;
using Domain.ValueObjects;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Application.Services;

public sealed class RutaService(
    IAppDbContext db,
    IPlanificadorRutas planificador,
    IValidator<ArmarRutasRequest> validador,
    INotificadorEventos notificador,
    TimeProvider reloj,
    ReglasPlanificacion reglas,
    EstimadorRutas estimador) : IRutaService
{
    public async Task<ArmadoRutasDto> ArmarRutasAsync(ArmarRutasRequest request, CancellationToken ct = default)
    {
        await validador.ValidateAndThrowAsync(request, ct);
        var hoyPlanificacion = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(reloj.GetUtcNow(),
            TimeZoneInfo.FindSystemTimeZoneById(reglas.ZonaHoraria)).DateTime);
        if (request.Fecha < hoyPlanificacion)
            throw new DomainException("No se pueden armar rutas para una fecha pasada.");

        var contexto = await ContextoArmadoAsync(request.Fecha, request.Origen, request.SalidaProgramadaEn, ct);

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
                : planificador.Planificar(zona, pendientes, libres, contexto);

            foreach (var asignacion in asignaciones.Where(a => a.PedidosEnOrden.Count > 0))
            {
                var ruta = Ruta.Crear(zona, asignacion.Repartidor, request.Fecha, reloj.GetUtcNow());
                foreach (var pedido in asignacion.PedidosEnOrden)
                    ruta.AgregarPedido(pedido);
                if (asignacion.Estimacion is { } estimacion)
                    ruta.AplicarEstimacion(estimacion, reloj.GetUtcNow());

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
        foreach (var pedido in creadas.SelectMany(r => r.Paradas).Select(p => p.Pedido))
            await notificador.PedidoActualizadoAsync(pedido.ToDto(), ct);

        return new ArmadoRutasDto(request.Fecha, dtos, sinAsignar);
    }

    /// <summary>Origen del armado: el enviado por el panel o, si no llega, el local guardado/configurado.</summary>
    private async Task<ContextoPlanificacion?> ContextoArmadoAsync(DateOnly fecha, PuntoDto? origen,
        DateTimeOffset? salida, CancellationToken ct)
    {
        GeoPoint? punto = origen is { } o ? new GeoPoint(o.Latitud, o.Longitud) : await estimador.OrigenLocalAsync(ct);
        return punto is { } p ? reglas.CrearContexto(fecha, p, reloj.GetUtcNow(), salida) : null;
    }

    public async Task<ReoptimizacionDto> ReoptimizarAsync(ReoptimizarRutasRequest request, CancellationToken ct = default)
    {
        if (request.Fecha == default) throw new DomainException("Indique la fecha a reoptimizar.");
        if (request.Fecha < reglas.FechaLocal(reloj.GetUtcNow()))
            throw new DomainException("No se pueden reoptimizar rutas de una fecha pasada.");
        var contexto = await ContextoArmadoAsync(request.Fecha, request.Origen, null, ct);

        // Al aplicar: una transacción con el mismo bloqueo por fecha que el checkout, para no
        // chocar con pedidos o reservas que entran mientras se rearman las rutas.
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? tx = null;
        try
        {
            if (request.Aplicar && db is DbContext contexto0 && contexto0.Database.IsRelational())
                tx = await contexto0.Database.BeginTransactionAsync(ct);
            if (request.Aplicar) await db.BloquearPlanificacionAsync(request.Fecha, ct);

            var zonas = await db.Zonas
                .Where(z => z.Activa && (request.ZonaId == null || z.Id == request.ZonaId))
                .OrderBy(z => z.Nombre)
                .ToListAsync(ct);
            if (request.ZonaId is { } zonaId && zonas.Count == 0)
                throw new NoEncontradoException(nameof(Zona), zonaId);
            var zonaIds = zonas.Select(z => z.Id).ToList();

            // Solo rutas planificadas: las que ya salieron (EnCurso) o terminaron no se tocan.
            var planificadas = await db.Rutas.ConParadas()
                .Where(r => r.Fecha == request.Fecha && r.Estado == EstadoRuta.Planificada && zonaIds.Contains(r.ZonaId))
                .ToListAsync(ct);
            var antes = planificadas
                .SelectMany(r => r.Paradas.Select(p => (PedidoId: p.Pedido.Id, r.RepartidorId)))
                .ToDictionary(x => x.PedidoId, x => x.RepartidorId);
            var pedidosPlanificados = planificadas.SelectMany(r => r.Paradas).Select(p => p.Pedido).ToList();
            var repartidoresPlan = planificadas.Select(r => r.RepartidorId).ToHashSet();

            var libres = await db.RepartidoresLibresAsync(request.Fecha, ct);
            var propios = await db.Repartidores
                .Where(r => repartidoresPlan.Contains(r.Id) && r.Estado == EstadoRepartidor.Disponible)
                .ToListAsync(ct);
            var disponibles = libres.Concat(propios).DistinctBy(r => r.Id).OrderBy(r => r.Id).ToList();

            var pendientes = await db.Pedidos
                .Where(p => p.FechaEntrega == request.Fecha && zonaIds.Contains(p.ZonaId) && p.Estado == EstadoPedido.Pendiente)
                .ToListAsync(ct);

            if (request.Aplicar)
            {
                foreach (var ruta in planificadas) ruta.Cancelar(); // devuelve sus pedidos a pendientes
                await db.GuardarAsync(ct);
            }

            var todos = pendientes.Concat(pedidosPlanificados).DistinctBy(p => p.Id).ToList();
            var propuestas = new List<RutaPropuestaDto>();
            var creadas = new List<Ruta>();
            int sinAsignar = 0, quedanSinRuta = 0;
            foreach (var zona in zonas)
            {
                var deZona = todos.Where(p => p.ZonaId == zona.Id).OrderBy(p => p.HoraEntrega).ThenBy(p => p.Id).ToList();
                if (deZona.Count == 0) continue;
                var asignaciones = disponibles.Count == 0 ? [] : planificador.Planificar(zona, deZona, disponibles, contexto);
                var asignados = new HashSet<Guid>();
                foreach (var asignacion in asignaciones.Where(a => a.PedidosEnOrden.Count > 0))
                {
                    var estimadas = asignacion.Estimacion?.Paradas.ToDictionary(p => p.PedidoId);
                    propuestas.Add(new RutaPropuestaDto(zona.Id, asignacion.Repartidor.Id, asignacion.Repartidor.Nombre,
                        Math.Min(zona.MaxPedidosPorRuta, asignacion.Repartidor.CapacidadMaxima),
                        asignacion.PedidosEnOrden.Select((p, i) =>
                        {
                            var e = estimadas?.GetValueOrDefault(p.Id);
                            return new ParadaPropuestaDto(p.Id, i + 1, p.ClienteNombre, p.Direccion,
                                p.CompromisoInicioEn, p.CompromisoFinEn, e?.FinalizacionEn, e?.ExcedeCompromiso ?? false,
                                antes.TryGetValue(p.Id, out var previo) && previo != asignacion.Repartidor.Id);
                        }).ToList(),
                        asignacion.Estimacion?.DistanciaEstimadaKm));
                    foreach (var p in asignacion.PedidosEnOrden) asignados.Add(p.Id);

                    if (request.Aplicar)
                    {
                        var ruta = Ruta.Crear(zona, asignacion.Repartidor, request.Fecha, reloj.GetUtcNow());
                        foreach (var pedido in asignacion.PedidosEnOrden) ruta.AgregarPedido(pedido);
                        if (asignacion.Estimacion is { } estimacion) ruta.AplicarEstimacion(estimacion, reloj.GetUtcNow());
                        db.Rutas.Add(ruta);
                        creadas.Add(ruta);
                    }
                    disponibles.Remove(asignacion.Repartidor); // un repartidor, una ruta por armado
                }
                var noAsignados = deZona.Where(p => !asignados.Contains(p.Id)).ToList();
                sinAsignar += noAsignados.Count;
                quedanSinRuta += noAsignados.Count(p => antes.ContainsKey(p.Id));
            }

            var dtos = new List<RutaDto>();
            if (request.Aplicar)
            {
                await db.GuardarAsync(ct);
                if (tx is not null) await tx.CommitAsync(ct);
                foreach (var ruta in planificadas) await notificador.RutaActualizadaAsync(ruta.ToDto(), ct);
                dtos = creadas.Select(r => r.ToDto()).ToList();
                foreach (var dto in dtos) await notificador.RutaActualizadaAsync(dto, ct);
                foreach (var pedido in todos) await notificador.PedidoActualizadoAsync(pedido.ToDto(), ct);
            }

            var paradas = propuestas.SelectMany(p => p.Paradas).ToList();
            return new ReoptimizacionDto(request.Fecha, request.Aplicar, planificadas.Count, propuestas, sinAsignar,
                paradas.Count(p => p.CambiaDeRepartidor), quedanSinRuta, paradas.Count(p => p.ExcedeCompromiso), dtos);
        }
        finally
        {
            if (tx is not null) await tx.DisposeAsync();
        }
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

    public async Task<RutaDto> AgregarPedidoAsync(Guid rutaId, Guid pedidoId, int? posicion, CancellationToken ct = default, bool forzar = false)
    {
        var ruta = await CargarAsync(rutaId, ct);
        var pedido = await db.Pedidos.FirstOrDefaultAsync(p => p.Id == pedidoId, ct)
                     ?? throw new NoEncontradoException(nameof(Pedido), pedidoId);

        await estimador.ValidarCambioAsync([ruta], () => ruta.AgregarPedido(pedido, posicion), forzar, ct);
        var dto = await GuardarYNotificarAsync(ruta, ct);
        await notificador.PedidoActualizadoAsync(pedido.ToDto(), ct);
        return dto;
    }

    public async Task<RutaDto> QuitarPedidoAsync(Guid rutaId, Guid pedidoId, CancellationToken ct = default)
    {
        var ruta = await CargarAsync(rutaId, ct);
        var pedido = ruta.Paradas.FirstOrDefault(p => p.PedidoId == pedidoId)?.Pedido;

        ruta.QuitarPedido(pedidoId);
        await estimador.ReestimarAsync(ruta, ct); // quitar una parada no retrasa a las demás
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
        await estimador.ReestimarAsync(ruta, ct);
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

    public async Task<RutaDto> ReordenarAsync(Guid rutaId, ReordenarParadasRequest r, CancellationToken ct)
    {
        var ruta = await CargarAsync(rutaId, ct);
        if (ruta.Version != r.Version) throw new ConflictoException("La ruta cambió. Recargue antes de reordenar.");
        await estimador.ValidarCambioAsync([ruta], () => ruta.ReordenarParadas(r.PedidosEnOrden), r.Forzar, ct);
        return await GuardarYNotificarAsync(ruta, ct);
    }

    public async Task<TransferenciaPedidoDto> TransferirAsync(Guid rutaId, Guid pedidoId, TransferirPedidoRequest r, CancellationToken ct)
    {
        var origen = await CargarAsync(rutaId, ct);
        var destino = await CargarAsync(r.DestinoRutaId, ct);
        if (origen.Version != r.VersionOrigen || destino.Version != r.VersionDestino)
            throw new ConflictoException("Una ruta cambió. Recargue ambas antes de transferir.");
        var pedido = origen.Paradas.FirstOrDefault(p => p.PedidoId == pedidoId)?.Pedido
            ?? throw new NoEncontradoException("Pedido en ruta", pedidoId);
        await estimador.ValidarCambioAsync([origen, destino],
            () => origen.TransferirPedidoA(destino, pedidoId, r.Posicion), r.Forzar, ct);
        // Un solo SaveChanges: EF Core usa una transacción para las dos rutas y sus paradas.
        await db.GuardarAsync(ct);
        var salida = new TransferenciaPedidoDto(origen.ToDto(), destino.ToDto());
        await notificador.RutaActualizadaAsync(salida.Origen, ct);
        await notificador.RutaActualizadaAsync(salida.Destino, ct);
        await notificador.PedidoActualizadoAsync(pedido.ToDto(), ct);
        return salida;
    }

    public async Task<RutaDto> RecalcularEstimacionesAsync(Guid rutaId, CancellationToken ct)
    {
        var ruta = await CargarAsync(rutaId, ct);
        if (await estimador.ReestimarAsync(ruta, ct) is null)
            throw new DomainException("No se puede estimar la ruta: no está activa, no tiene paradas restantes o no hay un origen conocido.");
        return await GuardarYNotificarAsync(ruta, ct);
    }

    private async Task<RutaDto> GuardarYNotificarAsync(Ruta ruta, CancellationToken ct)
    {
        await db.GuardarAsync(ct);
        var dto = ruta.ToDto();
        await notificador.RutaActualizadaAsync(dto, ct);
        return dto;
    }
}
