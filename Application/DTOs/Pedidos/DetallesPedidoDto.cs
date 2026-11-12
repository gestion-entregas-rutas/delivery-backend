using Domain.Enums;
namespace Application.DTOs;

/// <summary>Información adicional del pedido; null significa no informado, incluido en datos históricos.</summary>
public sealed record DetallesPedidoDto(string? RemitenteNombre, string? RemitenteTelefono,
    string? RemitenteCorreo, string? DestinatarioCorreo, string? Observaciones,
    TipoCarga? TipoCarga, decimal? PesoKg, bool? Fragil, PrioridadPedido? Prioridad);
