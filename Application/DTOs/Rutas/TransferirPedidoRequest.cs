namespace Application.DTOs;
/// <summary>Transferencia atómica entre rutas compatibles. Posicion usa índice base cero.</summary>
public sealed record TransferirPedidoRequest(Guid DestinoRutaId, uint VersionOrigen, uint VersionDestino, int? Posicion = null);
