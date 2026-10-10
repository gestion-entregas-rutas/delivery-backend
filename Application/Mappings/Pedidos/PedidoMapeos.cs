using Application.DTOs;
using Domain.Entities;

namespace Application.Mappings;

public static class PedidoMapeos
{
    public static PedidoDto ToDto(this Pedido pedido) => new
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
        pedido.EntregadoEn);
}
