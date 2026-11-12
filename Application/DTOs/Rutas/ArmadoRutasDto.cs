
namespace Application.DTOs;

public sealed record ArmadoRutasDto(DateOnly Fecha, IReadOnlyList<RutaDto> Rutas, int PedidosSinAsignar);
