
namespace Application.DTOs;

public sealed record RegistroPedidoDto(PedidoDto Pedido, ResultadoAsignacionDto? Asignacion);
