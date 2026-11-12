using Domain.Enums;
namespace Application.DTOs;

/// <summary>Datos opcionales; el peso se expresa en kilogramos. No modifican aún el algoritmo de ruteo.</summary>
public sealed record DetallesPedidoRequest(string? RemitenteNombre = null, string? RemitenteTelefono = null,
    string? RemitenteCorreo = null, string? DestinatarioCorreo = null, string? Observaciones = null,
    TipoCarga? TipoCarga = null, decimal? PesoKg = null, bool? Fragil = null, PrioridadPedido? Prioridad = null);
