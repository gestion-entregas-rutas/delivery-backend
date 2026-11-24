
namespace Application.DTOs;

public sealed record ParadaDto(int Orden, PedidoDto Pedido,
    DateTimeOffset? LlegadaEstimadaEn = null,
    DateTimeOffset? InicioAtencionEstimadoEn = null,
    DateTimeOffset? FinalizacionEstimadaEn = null,
    double? TrasladoEstimadoMinutos = null);
