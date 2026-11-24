using Application.DTOs;

namespace Application.Interfaces;

public interface IRutaService
{
    /// <summary>Arma las rutas de una fecha (y zona) con todos los pedidos pendientes.</summary>
    Task<ArmadoRutasDto> ArmarRutasAsync(ArmarRutasRequest request, CancellationToken ct = default);

    /// <summary>Rearma las rutas planificadas junto con los pendientes. Sin <c>Aplicar</c> solo devuelve la vista previa.</summary>
    Task<ReoptimizacionDto> ReoptimizarAsync(ReoptimizarRutasRequest request, CancellationToken ct = default);
    Task<RutaDto> ObtenerAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<RutaDto>> ListarAsync(DateOnly fecha, Guid? zonaId, CancellationToken ct = default);

    // Ajustes manuales del personal del negocio
    Task<RutaDto> AgregarPedidoAsync(Guid rutaId, Guid pedidoId, int? posicion, CancellationToken ct = default, bool forzar = false);
    Task<RutaDto> QuitarPedidoAsync(Guid rutaId, Guid pedidoId, CancellationToken ct = default);
    Task<RutaDto> IniciarAsync(Guid rutaId, CancellationToken ct = default);
    Task<RutaDto> CancelarAsync(Guid rutaId, CancellationToken ct = default);
    Task<RutaDto> ReordenarAsync(Guid rutaId, ReordenarParadasRequest request, CancellationToken ct);
    Task<TransferenciaPedidoDto> TransferirAsync(Guid rutaId, Guid pedidoId, TransferirPedidoRequest request, CancellationToken ct);

    /// <summary>Recalcula las estimaciones con la posición/avance actuales (GPS, entregas registradas).</summary>
    Task<RutaDto> RecalcularEstimacionesAsync(Guid rutaId, CancellationToken ct);
}
