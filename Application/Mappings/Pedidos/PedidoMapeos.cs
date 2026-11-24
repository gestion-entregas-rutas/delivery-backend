using Application.DTOs;
using Domain.Entities;
using Domain.Services;

namespace Application.Mappings;

public static class PedidoMapeos
{
    public static PedidoDto ToDto(this Pedido pedido, DateTimeOffset? ahora = null,
        DateTimeOffset? finalizacionEstimada = null) => new
        (
        pedido.Id, 
        pedido.ClienteNombre, 
        pedido.ClienteTelefono, 
        pedido.Direccion, 
        pedido.Ubicacion.ToDto(), 
        pedido.ZonaId,
        pedido.FechaEntrega, 
        pedido.HoraEntrega, 
        pedido.Estado, 
        pedido.RutaId, 
        pedido.RegistradoEn, 
        pedido.EntregadoEn, pedido.Detalles.ToDto(), pedido.Version,
        pedido.CompromisoInicioEn, pedido.CompromisoFinEn, pedido.TiempoServicioMinutos,
        pedido.Estado == Domain.Enums.EstadoPedido.Cancelado ? new ResultadoPuntualidad("NoAplica", null) :
        Puntualidad.Calcular(pedido.CompromisoInicioEn, pedido.CompromisoFinEn,
            pedido.EntregadoEn, finalizacionEstimada, ahora ?? DateTimeOffset.UtcNow));
}
