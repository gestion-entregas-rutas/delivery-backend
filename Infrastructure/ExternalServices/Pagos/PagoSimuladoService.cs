using Application.Interfaces;

namespace Infrastructure.ExternalServices;

public sealed class PagoSimuladoService : IPagoService
{
    public string CrearReferenciaSimulada(Guid ordenId, string? metodo, string? referenciaSolicitada)
        => string.IsNullOrWhiteSpace(referenciaSolicitada) ? $"SIM-{ordenId:N}-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}" : referenciaSolicitada.Trim();

    public bool ConfirmarSimulado(string? referencia, string? referenciaEsperada)
        => true; // simulación: confirmación válida si llega al endpoint
}
