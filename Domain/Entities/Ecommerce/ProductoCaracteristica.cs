using Domain.Common;

namespace Domain.Entities;

public sealed class ProductoCaracteristica : Entity
{
    private ProductoCaracteristica() { }
    public Guid ProductoId { get; private set; }
    public string Texto { get; private set; } = null!;
    public int Orden { get; private set; }

    public static ProductoCaracteristica Crear(Guid productoId, string texto, int orden) =>
        new() { ProductoId = productoId, Texto = (texto ?? "").Trim(), Orden = orden };
}
