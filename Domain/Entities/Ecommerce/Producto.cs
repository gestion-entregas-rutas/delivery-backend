using Domain.Common;
using Domain.Exceptions;

namespace Domain.Entities;

public sealed class Producto : Entity
{
    private Producto() { }
    public Guid CategoriaId { get; private set; }
    public Guid? SubcategoriaId { get; private set; }
    public string SubcategoriaTexto { get; private set; } = "";
    public string Nombre { get; private set; } = null!;
    public string Marca { get; private set; } = "";
    public string DescripcionCorta { get; private set; } = "";
    public decimal PrecioBase { get; private set; }
    public decimal? PrecioOferta { get; private set; }
    public int Stock { get; private set; }
    public bool Activo { get; private set; } = true;
    public string ImagenPrincipalUrl { get; private set; } = "";
    public double CalificacionPromedio { get; private set; }
    public int TotalResenas { get; private set; }

    public static Producto Crear(Guid categoriaId, Guid? subcategoriaId, string subcategoriaTexto, string nombre, string? marca,
        string? descripcionCorta, decimal precioBase, decimal? precioOferta, int stock, string? imagenPrincipalUrl)
    {
        if (categoriaId == Guid.Empty) throw new DomainException("El producto requiere una categoría.");
        if (string.IsNullOrWhiteSpace(nombre)) throw new DomainException("El producto debe tener nombre.");
        if (precioBase < 0) throw new DomainException("El precio base no puede ser negativo.");
        if (precioOferta is < 0) throw new DomainException("El precio de oferta no puede ser negativo.");
        if (precioOferta.HasValue && precioOferta.Value > precioBase) throw new DomainException("La oferta no puede ser mayor al precio base.");
        if (stock < 0) throw new DomainException("El stock no puede ser negativo.");
        return new Producto
        {
            CategoriaId = categoriaId,
            SubcategoriaId = subcategoriaId,
            SubcategoriaTexto = (subcategoriaTexto ?? "").Trim(),
            Nombre = nombre.Trim(),
            Marca = (marca ?? "").Trim(),
            DescripcionCorta = (descripcionCorta ?? "").Trim(),
            PrecioBase = precioBase,
            PrecioOferta = precioOferta,
            Stock = stock,
            Activo = true,
            ImagenPrincipalUrl = (imagenPrincipalUrl ?? "").Trim()
        };
    }

    public void Actualizar(Guid categoriaId, Guid? subcategoriaId, string subcategoriaTexto, string nombre, string? marca,
        string? descripcionCorta, decimal precioBase, decimal? precioOferta, string? imagenPrincipalUrl, bool activo)
    {
        if (categoriaId == Guid.Empty) throw new DomainException("El producto requiere una categoría.");
        if (string.IsNullOrWhiteSpace(nombre)) throw new DomainException("El producto debe tener nombre.");
        if (precioBase < 0) throw new DomainException("El precio base no puede ser negativo.");
        if (precioOferta is < 0) throw new DomainException("El precio de oferta no puede ser negativo.");
        if (precioOferta.HasValue && precioOferta.Value > precioBase) throw new DomainException("La oferta no puede ser mayor al precio base.");
        CategoriaId = categoriaId;
        SubcategoriaId = subcategoriaId;
        SubcategoriaTexto = (subcategoriaTexto ?? "").Trim();
        Nombre = nombre.Trim();
        Marca = (marca ?? "").Trim();
        DescripcionCorta = (descripcionCorta ?? "").Trim();
        PrecioBase = precioBase;
        PrecioOferta = precioOferta;
        ImagenPrincipalUrl = (imagenPrincipalUrl ?? "").Trim();
        Activo = activo;
    }

    public void AjustarStock(int stock)
    {
        if (stock < 0) throw new DomainException("El stock no puede ser negativo.");
        Stock = stock;
    }

    public void Desactivar() => Activo = false;
    public void Activar() => Activo = true;

    public void ActualizarCalificacion(double promedio, int total)
    {
        CalificacionPromedio = promedio;
        TotalResenas = total;
    }
}
