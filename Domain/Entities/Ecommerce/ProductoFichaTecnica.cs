using Domain.Common;

namespace Domain.Entities;

public sealed class ProductoFichaTecnica : Entity
{
    private ProductoFichaTecnica() { }
    public Guid ProductoId { get; private set; }
    public string Clave { get; private set; } = null!;
    public string Valor { get; private set; } = null!;
    public int Orden { get; private set; }

    public static ProductoFichaTecnica Crear(Guid productoId, string clave, string valor, int orden) =>
        new() { ProductoId = productoId, Clave = (clave ?? "").Trim(), Valor = (valor ?? "").Trim(), Orden = orden };
}
