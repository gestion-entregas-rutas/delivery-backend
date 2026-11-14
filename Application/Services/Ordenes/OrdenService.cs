using System.Text.Json;
using Application.Common;
using Application.DTOs;
using Application.Exceptions;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Domain.Exceptions;
using Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Application.Services;

public sealed class OrdenService(IAppDbContext db, TimeProvider reloj, IPagoService pagos) : IOrdenService
{
    private static readonly TimeSpan ExpiracionReserva = TimeSpan.FromMinutes(15);

    public async Task<CheckoutResponse> CheckoutAsync(Guid clienteId, CheckoutRequest request, string? idempotencyKey, CancellationToken ct)
    {
        if (request.Items is null || request.Items.Count == 0)
            throw new DomainException("Debe incluir al menos un producto.");
        if (request.Items.Any(i => i.Cantidad <= 0))
            throw new DomainException("Las cantidades deben ser mayores a 0.");

        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var existente = await db.IdempotencyRecords.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Key == idempotencyKey && x.Operacion == "checkout" && x.UsuarioId == clienteId, ct);
            if (existente?.ResultadoJson is not null)
                return JsonSerializer.Deserialize<CheckoutResponse>(existente.ResultadoJson)!;
        }

        var tipoEntrega = ParseTipoEntrega(request.TipoEntrega);
        if (tipoEntrega == TipoEntrega.Domicilio && request.DatosDomicilio is null)
            throw new DomainException("Los datos de domicilio son obligatorios.");
        if (tipoEntrega == TipoEntrega.RetiroTienda && request.DatosRetiroTienda is null)
            throw new DomainException("Los datos de retiro en tienda son obligatorios.");

        await using var tx = await ((DbContext)db).Database.BeginTransactionAsync(ct);
        try
        {
            if (!string.IsNullOrWhiteSpace(idempotencyKey))
            {
                var existente = await db.IdempotencyRecords.FirstOrDefaultAsync(x => x.Key == idempotencyKey && x.Operacion == "checkout" && x.UsuarioId == clienteId, ct);
                if (existente?.ResultadoJson is not null)
                {
                    await tx.RollbackAsync(ct);
                    return JsonSerializer.Deserialize<CheckoutResponse>(existente.ResultadoJson)!;
                }
            }

            var ids = request.Items.Select(i => i.ProductoId).Distinct().ToList();
            var productos = await db.Productos.Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
            foreach (var id in ids)
                if (!productos.ContainsKey(id)) throw new NoEncontradoException(nameof(Producto), id);

            decimal total = 0;
            var detallesCalc = new List<(Producto p, int cantidad, decimal precio)>();
            foreach (var item in request.Items)
            {
                var p = productos[item.ProductoId];
                if (!p.Activo) throw new DomainException($"El producto '{p.Nombre}' no está disponible.");
                var precio = p.PrecioOferta ?? p.PrecioBase;
                total += precio * item.Cantidad;
                detallesCalc.Add((p, item.Cantidad, precio));
            }

            if (tipoEntrega == TipoEntrega.Domicilio)
            {
                var d = request.DatosDomicilio!;
                var punto = new GeoPoint(d.Latitud, d.Longitud);
                var zonas = await db.Zonas.Where(z => z.Activa).ToListAsync(ct);
                if (!zonas.Any(z => z.Contiene(punto)))
                    throw new DomainException("La dirección no tiene cobertura de entrega en una zona activa.");
                if (d.FechaEntrega < reloj.Hoy())
                    throw new DomainException("La fecha de entrega no puede estar en el pasado.");
            }

            // Reserva atómica de stock (descuento todo-o-nada)
            foreach (var (p, cantidad, _) in detallesCalc)
            {
                var filas = await ((DbContext)db).Database.ExecuteSqlRawAsync(
                    "UPDATE \"Productos\" SET \"Stock\" = \"Stock\" - {0} WHERE \"Id\" = {1} AND \"Activo\" = TRUE AND \"Stock\" >= {0}",
                    new object[] { cantidad, p.Id }, ct);
                if (filas == 0)
                {
                    var actual = await db.Productos.AsNoTracking().Where(x => x.Id == p.Id).Select(x => new { x.Stock, x.Nombre }).FirstOrDefaultAsync(ct);
                    throw new ConflictoException(
                        $"El producto '{actual?.Nombre ?? p.Nombre}' ya no cuenta con stock suficiente o se ha agotado mientras realizabas tu compra.",
                        "STOCK_AGOTADO",
                        new Dictionary<string, object?> { ["productoId"] = p.Id, ["stockDisponible"] = actual?.Stock ?? 0 });
                }
            }

            var ahora = reloj.GetUtcNow();
            var expira = ahora.Add(ExpiracionReserva);
            var numero = $"ORD-{ahora:yyyyMMddHHmmss}-{Random.Shared.Next(100, 999)}";
            var orden = Orden.Crear(numero, clienteId, tipoEntrega, total, ahora, idempotencyKey);
            string? codigoRetiro = null;
            string? estadoSeguimiento = null;

            if (tipoEntrega == TipoEntrega.RetiroTienda)
            {
                var r = request.DatosRetiroTienda!;
                codigoRetiro = $"RET-{Random.Shared.Next(100000, 999999)}";
                orden.ConfigurarRetiro(r.CedulaQuienRetira.Trim(), r.NombreQuienRetira.Trim(), r.TelefonoContacto.Trim(), r.FechaRetiro, r.FranjaHoraria.Trim(), codigoRetiro);
                estadoSeguimiento = "PendientePago";
            }
            else
            {
                var d = request.DatosDomicilio!;
                orden.ConfigurarDomicilio(d.DestinatarioNombre.Trim(), d.DestinatarioTelefono.Trim(), d.DireccionTexto.Trim(), d.Latitud, d.Longitud, d.FechaEntrega, d.HoraEntrega, d.Observaciones);
                estadoSeguimiento = "PendientePago";
            }

            db.Ordenes.Add(orden);
            foreach (var (p, cantidad, precio) in detallesCalc)
                db.DetalleOrdenes.Add(DetalleOrden.Crear(orden.Id, p.Id, p.Nombre, precio, cantidad));

            var referencia = pagos.CrearReferenciaSimulada(orden.Id, request.MetodoPago?.Tipo, request.MetodoPago?.ReferenciaSimulada);
            var pago = PagoOrden.Crear(orden.Id, request.MetodoPago?.Tipo ?? "Simulado", referencia, EstadoPago.Pendiente, ahora, expira);
            db.PagosOrden.Add(pago);

            await db.SaveChangesAsync(ct);

            var response = new CheckoutResponse(orden.Id, orden.NumeroOrden, orden.Estado.ToString(), orden.EstadoPago.ToString(), orden.Total, orden.TipoEntrega.ToString(), codigoRetiro ?? orden.CodigoRetiro, orden.PedidoLogisticoId, estadoSeguimiento, pago.Id, pago.Referencia, pago.ExpiraEn);

            if (!string.IsNullOrWhiteSpace(idempotencyKey))
            {
                db.IdempotencyRecords.Add(IdempotencyRecord.Crear(idempotencyKey, clienteId, "checkout", orden.Id, ahora, JsonSerializer.Serialize(response)));
                await db.SaveChangesAsync(ct);
            }

            var carrito = await db.Carritos.FirstOrDefaultAsync(c => c.UsuarioId == clienteId, ct);
            if (carrito is not null)
            {
                var items = await db.CarritoItems.Where(i => i.CarritoId == carrito.Id).ToListAsync(ct);
                db.CarritoItems.RemoveRange(items);
                carrito.Tocar(ahora);
                await db.SaveChangesAsync(ct);
            }

            await tx.CommitAsync(ct);
            return response;
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<CheckoutResponse> ConfirmarPagoAsync(Guid clienteId, Guid ordenId, string? referencia, string? idempotencyKey, CancellationToken ct)
    {
        var op = $"confirmar-pago:{ordenId}";
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var existente = await db.IdempotencyRecords.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Key == idempotencyKey && x.Operacion == op && x.UsuarioId == clienteId, ct);
            if (existente?.ResultadoJson is not null)
                return JsonSerializer.Deserialize<CheckoutResponse>(existente.ResultadoJson)!;
        }

        await using var tx = await ((DbContext)db).Database.BeginTransactionAsync(ct);
        try
        {
            if (!string.IsNullOrWhiteSpace(idempotencyKey))
            {
                var existente = await db.IdempotencyRecords.FirstOrDefaultAsync(x => x.Key == idempotencyKey && x.Operacion == op && x.UsuarioId == clienteId, ct);
                if (existente?.ResultadoJson is not null)
                {
                    await tx.RollbackAsync(ct);
                    return JsonSerializer.Deserialize<CheckoutResponse>(existente.ResultadoJson)!;
                }
            }

            var orden = await db.Ordenes.FirstOrDefaultAsync(o => o.Id == ordenId && o.ClienteId == clienteId, ct)
                ?? throw new NoEncontradoException(nameof(Orden), ordenId);
            var pago = await db.PagosOrden.FirstOrDefaultAsync(p => p.OrdenId == orden.Id, ct)
                ?? throw new DomainException("La orden no tiene un pago asociado.");
            var ahora = reloj.GetUtcNow();

            if (pago.Estado == EstadoPago.Confirmado)
            {
                var respOk = await ConstruirRespuestaAsync(orden, pago, ct);
                await tx.CommitAsync(ct);
                return respOk;
            }

            if (pago.Estado is EstadoPago.Expirado or EstadoPago.Cancelado or EstadoPago.Fallido || (pago.ExpiraEn.HasValue && pago.ExpiraEn.Value < ahora))
            {
                if (pago.Estado == EstadoPago.Pendiente)
                {
                    await ExpirarYLiberarAsync(orden, pago, ahora, ct);
                    await db.SaveChangesAsync(ct);
                }
                await tx.CommitAsync(ct);
                throw new ConflictoException("La reserva de pago expiró o fue liberada; no se puede confirmar una confirmación tardía.", "PAGO_NO_CONFIRMABLE");
            }

            if (!pagos.ConfirmarSimulado(referencia ?? pago.Referencia, pago.Referencia))
                throw new ConflictoException("La confirmación de pago no es válida.", "PAGO_INVALIDO");

            pago.Confirmar(ahora);
            Guid? pedidoLogisticoId = orden.PedidoLogisticoId;
            string? estadoSeguimiento = null;

            if (orden.TipoEntrega == TipoEntrega.Domicilio)
            {
                if (pedidoLogisticoId is null)
                {
                    var lat = orden.Latitud ?? throw new DomainException("Faltan coordenadas de entrega.");
                    var lon = orden.Longitud ?? throw new DomainException("Faltan coordenadas de entrega.");
                    var punto = new GeoPoint(lat, lon);
                    var zonas = await db.Zonas.Where(z => z.Activa).ToListAsync(ct);
                    var zona = zonas.FirstOrDefault(z => z.Contiene(punto)) ?? throw new DomainException("La dirección no tiene cobertura de entrega en una zona activa.");
                    var resumen = await db.DetalleOrdenes.Where(d => d.OrdenId == orden.Id)
                        .Select(d => $"{d.Cantidad}x {d.NombreProducto}").ToListAsync(ct);
                    var detallesPedido = InformacionPedido.Crear(null, null, null, null, string.Join(", ", resumen), null, null, null, null);
                    var pedido = Pedido.Registrar(orden.DestinatarioNombre!, orden.DestinatarioTelefono!, orden.DireccionTexto!,
                        punto, zona, orden.FechaEntrega ?? reloj.Hoy(), orden.HoraEntrega ?? new TimeOnly(12, 0), reloj.Hoy(), ahora, detallesPedido);
                    db.Pedidos.Add(pedido);
                    pedidoLogisticoId = pedido.Id;
                    estadoSeguimiento = pedido.Estado.ToString();
                }
                else
                {
                    var pedido = await db.Pedidos.AsNoTracking().FirstOrDefaultAsync(p => p.Id == pedidoLogisticoId.Value, ct);
                    estadoSeguimiento = pedido?.Estado.ToString() ?? "Pendiente";
                }
            }
            else
            {
                estadoSeguimiento = "ListoParaRetirar";
            }

            orden.MarcarPagada(pedidoLogisticoId);
            await db.SaveChangesAsync(ct);

            var response = await ConstruirRespuestaAsync(orden, pago, ct);
            if (!string.IsNullOrWhiteSpace(idempotencyKey))
            {
                db.IdempotencyRecords.Add(IdempotencyRecord.Crear(idempotencyKey, clienteId, op, orden.Id, ahora, JsonSerializer.Serialize(response)));
                await db.SaveChangesAsync(ct);
            }
            await tx.CommitAsync(ct);
            return response;
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<CheckoutResponse> CancelarPagoAsync(Guid clienteId, Guid ordenId, string? motivo, CancellationToken ct)
    {
        await using var tx = await ((DbContext)db).Database.BeginTransactionAsync(ct);
        try
        {
            var orden = await db.Ordenes.FirstOrDefaultAsync(o => o.Id == ordenId && o.ClienteId == clienteId, ct)
                ?? throw new NoEncontradoException(nameof(Orden), ordenId);
            var pago = await db.PagosOrden.FirstOrDefaultAsync(p => p.OrdenId == orden.Id, ct)
                ?? throw new DomainException("La orden no tiene un pago asociado.");
            var ahora = reloj.GetUtcNow();

            if (pago.Estado == EstadoPago.Confirmado)
                throw new ConflictoException("El pago ya fue confirmado; no se puede cancelar.", "PAGO_YA_CONFIRMADO");
            if (pago.Estado == EstadoPago.Cancelado || pago.Estado == EstadoPago.Expirado || pago.Estado == EstadoPago.Fallido)
            {
                var resp = await ConstruirRespuestaAsync(orden, pago, ct);
                await tx.CommitAsync(ct);
                return resp;
            }

            await LiberarStockReservaAsync(orden.Id, ct);
            pago.Cancelar();
            orden.MarcarCancelada();
            await db.SaveChangesAsync(ct);
            var response = await ConstruirRespuestaAsync(orden, pago, ct);
            await tx.CommitAsync(ct);
            return response;
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<IReadOnlyList<OrdenMiaDto>> ListarMiasAsync(Guid clienteId, CancellationToken ct)
    {
        await ExpirarPendientesDeClienteAsync(clienteId, ct);
        var ordenes = await db.Ordenes.AsNoTracking().Where(o => o.ClienteId == clienteId).OrderByDescending(o => o.CreadaEn).ToListAsync(ct);
        var pedidoIds = ordenes.Where(o => o.PedidoLogisticoId.HasValue).Select(o => o.PedidoLogisticoId!.Value).Distinct().ToList();
        var pedidos = pedidoIds.Count == 0 ? new Dictionary<Guid, Pedido>() : await db.Pedidos.AsNoTracking().Where(p => pedidoIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
        return ordenes.Select(o =>
        {
            string? seguimiento = null;
            if (o.TipoEntrega == TipoEntrega.RetiroTienda)
                seguimiento = o.Estado == EstadoOrden.Retirada ? "Retirado" : (o.EstadoPago == EstadoPago.Confirmado ? "ListoParaRetirar" : "PendientePago");
            else if (o.PedidoLogisticoId.HasValue && pedidos.TryGetValue(o.PedidoLogisticoId.Value, out var p))
                seguimiento = p.Estado.ToString();
            else if (o.EstadoPago != EstadoPago.Confirmado)
                seguimiento = "PendientePago";
            return new OrdenMiaDto(o.Id, o.NumeroOrden, o.Estado.ToString(), o.EstadoPago.ToString(), o.Total, o.TipoEntrega.ToString(), o.CreadaEn, o.CodigoRetiro, o.PedidoLogisticoId, seguimiento);
        }).ToList();
    }

    public async Task<IReadOnlyList<RetiroTiendaDto>> ListarRetirosTiendaAsync(CancellationToken ct)
    {
        return await db.Ordenes.AsNoTracking()
            .Where(o => o.TipoEntrega == TipoEntrega.RetiroTienda && o.EstadoPago == EstadoPago.Confirmado)
            .OrderByDescending(o => o.CreadaEn)
            .Select(o => new RetiroTiendaDto(o.Id, o.NumeroOrden, o.Estado.ToString(), o.Total, o.CreadaEn, o.CodigoRetiro, o.RetiroCedula, o.RetiroNombre, o.RetiroTelefono, o.RetiroFecha, o.RetiroFranja))
            .ToListAsync(ct);
    }

    public async Task MarcarRetiradoAsync(Guid ordenId, CancellationToken ct)
    {
        var orden = await db.Ordenes.FirstOrDefaultAsync(o => o.Id == ordenId, ct) ?? throw new NoEncontradoException(nameof(Orden), ordenId);
        if (orden.TipoEntrega != TipoEntrega.RetiroTienda) throw new DomainException("La orden no es de retiro en tienda.");
        if (orden.EstadoPago != EstadoPago.Confirmado) throw new DomainException("La orden no está pagada.");
        orden.MarcarRetirada();
        await db.SaveChangesAsync(ct);
    }

    private async Task<CheckoutResponse> ConstruirRespuestaAsync(Orden orden, PagoOrden pago, CancellationToken ct)
    {
        string? seguimiento = null;
        if (orden.TipoEntrega == TipoEntrega.RetiroTienda)
            seguimiento = orden.Estado == EstadoOrden.Retirada ? "Retirado" : (orden.EstadoPago == EstadoPago.Confirmado ? "ListoParaRetirar" : "PendientePago");
        else if (orden.PedidoLogisticoId.HasValue)
        {
            var pedido = await db.Pedidos.AsNoTracking().FirstOrDefaultAsync(p => p.Id == orden.PedidoLogisticoId.Value, ct);
            seguimiento = pedido?.Estado.ToString() ?? (orden.EstadoPago == EstadoPago.Confirmado ? "Pendiente" : "PendientePago");
        }
        else seguimiento = orden.EstadoPago == EstadoPago.Confirmado ? "Pendiente" : "PendientePago";
        return new CheckoutResponse(orden.Id, orden.NumeroOrden, orden.Estado.ToString(), orden.EstadoPago.ToString(), orden.Total, orden.TipoEntrega.ToString(), orden.CodigoRetiro, orden.PedidoLogisticoId, seguimiento, pago.Id, pago.Referencia, pago.ExpiraEn);
    }

    private async Task ExpirarPendientesDeClienteAsync(Guid clienteId, CancellationToken ct)
    {
        var ahora = reloj.GetUtcNow();
        var pendientes = await db.PagosOrden.Where(p => p.Estado == EstadoPago.Pendiente && p.ExpiraEn != null && p.ExpiraEn < ahora)
            .Join(db.Ordenes.Where(o => o.ClienteId == clienteId), p => p.OrdenId, o => o.Id, (p, o) => new { p, o })
            .ToListAsync(ct);
        foreach (var x in pendientes)
        {
            await ExpirarYLiberarAsync(x.o, x.p, ahora, ct);
        }
        if (pendientes.Count > 0) await db.SaveChangesAsync(ct);
    }

    private async Task ExpirarYLiberarAsync(Orden orden, PagoOrden pago, DateTimeOffset ahora, CancellationToken ct)
    {
        if (pago.Estado != EstadoPago.Pendiente) return;
        await LiberarStockReservaAsync(orden.Id, ct);
        pago.Expirar();
        orden.MarcarExpirada();
    }

    private async Task LiberarStockReservaAsync(Guid ordenId, CancellationToken ct)
    {
        var detalles = await db.DetalleOrdenes.AsNoTracking().Where(d => d.OrdenId == ordenId).ToListAsync(ct);
        foreach (var d in detalles)
        {
            await ((DbContext)db).Database.ExecuteSqlRawAsync(
                "UPDATE \"Productos\" SET \"Stock\" = \"Stock\" + {0} WHERE \"Id\" = {1}",
                new object[] { d.Cantidad, d.ProductoId }, ct);
        }
    }

    private static TipoEntrega ParseTipoEntrega(string? tipo)
    {
        if (Enum.TryParse<TipoEntrega>(tipo, true, out var t)) return t;
        throw new DomainException("Tipo de entrega inválido.");
    }
}
