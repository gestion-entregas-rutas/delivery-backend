namespace Domain.Entities;

public class ParadaRuta
{
    private ParadaRuta() { } // EF Core

    internal ParadaRuta(Guid rutaId, Pedido pedido, int orden)
    {
        RutaId = rutaId;
        PedidoId = pedido.Id;
        Pedido = pedido;
        Orden = orden;
    }

    public Guid RutaId { get; private set; }
    public Guid PedidoId { get; private set; }
    public Pedido Pedido { get; private set; } = null!;

    /// <summary>Posición de la parada dentro de la ruta, empezando en 1.</summary>
    public int Orden { get; internal set; }
    public DateTimeOffset? LlegadaEstimadaEn { get; private set; }
    public DateTimeOffset? InicioAtencionEstimadoEn { get; private set; }
    public DateTimeOffset? FinalizacionEstimadaEn { get; private set; }
    public double? TrasladoEstimadoMinutos { get; private set; }

    internal void Estimar(Domain.Services.EstimacionParada estimacion)
    {
        LlegadaEstimadaEn = estimacion.LlegadaEn.ToUniversalTime();
        InicioAtencionEstimadoEn = estimacion.InicioAtencionEn.ToUniversalTime();
        FinalizacionEstimadaEn = estimacion.FinalizacionEn.ToUniversalTime();
        TrasladoEstimadoMinutos = estimacion.TrasladoMinutos;
    }

    internal void InvalidarEstimacion()
    {
        LlegadaEstimadaEn = InicioAtencionEstimadoEn = FinalizacionEstimadaEn = null;
        TrasladoEstimadoMinutos = null;
    }
}
