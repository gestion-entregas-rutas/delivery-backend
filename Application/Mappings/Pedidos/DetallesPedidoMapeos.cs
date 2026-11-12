using Application.DTOs;
using Domain.ValueObjects;
namespace Application.Mappings;

public static class DetallesPedidoMapeos
{
    public static InformacionPedido? ToDomain(this DetallesPedidoRequest? r) => r is null ? null :
        InformacionPedido.Crear(r.RemitenteNombre, r.RemitenteTelefono, r.RemitenteCorreo,
            r.DestinatarioCorreo, r.Observaciones, r.TipoCarga, r.PesoKg, r.Fragil, r.Prioridad);
    public static DetallesPedidoDto? ToDto(this InformacionPedido? d) => d is null ? null :
        new(d.RemitenteNombre, d.RemitenteTelefono, d.RemitenteCorreo, d.DestinatarioCorreo,
            d.Observaciones, d.TipoCarga, d.PesoKg, d.Fragil, d.Prioridad);
}
