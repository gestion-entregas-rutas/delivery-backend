using Domain.Common;

namespace Domain.Entities;

public sealed class DetalleOrden : Entity
{
    private DetalleOrden() { }
    public Guid OrdenId { get; private set; }
    public Guid ProductoId { get; private set; }
    public string NombreProducto { get; private set; } = null!;
    public decimal PrecioUnitario { get; private set; }
    public int Cantidad { get; private set; }
    public decimal Subtotal { get; private set; }

    public static DetalleOrden Crear(Guid ordenId, Guid productoId, string nombreProducto, decimal precioUnitario, int cantidad)
    {
        return new DetalleOrden
        {
            OrdenId = ordenId,
            ProductoId = productoId,
            NombreProducto = nombreProducto,
            PrecioUnitario = precioUnitario,
            Cantidad = cantidad,
            Subtotal = precioUnitario * cantidad
        };
    }
}
