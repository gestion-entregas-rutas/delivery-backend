namespace Application.DTOs;

/// <param name="Posicion">Índice base 0 donde insertar la parada; si es null se agrega al final.</param>
/// <param name="Forzar">Aceptar aunque el cambio ponga entregas en riesgo (si no, 409 CAMBIO_PONE_EN_RIESGO).</param>
public sealed record AgregarPedidoARutaRequest(Guid PedidoId, int? Posicion = null, bool Forzar = false);
