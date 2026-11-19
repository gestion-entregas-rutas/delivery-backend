using Domain.Common;

namespace Domain.Entities;

public sealed class ProductoMedia : Entity
{
    private ProductoMedia() { }
    public Guid ProductoId { get; private set; }
    public string Tipo { get; private set; } = "imagen";
    public string Url { get; private set; } = null!;
    public int Orden { get; private set; }

    public static ProductoMedia Crear(Guid productoId, string tipo, string url, int orden) =>
        new() { ProductoId = productoId, Tipo = string.IsNullOrWhiteSpace(tipo) ? "imagen" : tipo.Trim(), Url = (url ?? "").Trim(), Orden = orden };
}
