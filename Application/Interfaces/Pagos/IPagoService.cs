namespace Application.Interfaces;

public interface IPagoService
{
    /// <summary>Simula el inicio de un pago y devuelve una referencia de proveedor simulada.</summary>
    string CrearReferenciaSimulada(Guid ordenId, string? metodo, string? referenciaSolicitada);
    /// <summary>Simula la confirmación del pago (siempre válida en modo simulado).</summary>
    bool ConfirmarSimulado(string? referencia, string? referenciaEsperada);
}
