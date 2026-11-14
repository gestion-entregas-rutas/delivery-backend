namespace Application.DTOs;

/// <param name="Posicion">Índice base 0 donde insertar la parada; si es null se agrega al final.</param>
public sealed record AgregarPedidoARutaRequest(Guid PedidoId, int? Posicion = null);
