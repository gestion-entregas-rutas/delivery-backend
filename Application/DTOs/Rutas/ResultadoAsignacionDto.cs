
namespace Application.DTOs;

/// <summary>Qué pasó con un pedido de último momento: se insertó, generó ruta nueva o quedó esperando.</summary>
public sealed record ResultadoAsignacionDto(TipoAsignacion Tipo, Guid? RutaId);
