using Domain.Common;

namespace Domain.Entities;

public sealed class CarritoItem : Entity
{
    private CarritoItem() { }
    public Guid CarritoId { get; private set; }
    public Guid ProductoId { get; private set; }
    public int Cantidad { get; private set; }

    public static CarritoItem Crear(Guid carritoId, Guid productoId, int cantidad) =>
        new() { CarritoId = carritoId, ProductoId = productoId, Cantidad = cantidad };

    public void CambiarCantidad(int cantidad) => Cantidad = cantidad;
}
