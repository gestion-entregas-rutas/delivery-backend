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
}
