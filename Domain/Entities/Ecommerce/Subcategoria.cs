using Domain.Common;
using Domain.Exceptions;

namespace Domain.Entities;

public sealed class Subcategoria : Entity
{
    private Subcategoria() { }
    public Guid CategoriaId { get; private set; }
    public string Nombre { get; private set; } = null!;
    public bool Activa { get; private set; } = true;

    public static Subcategoria Crear(Guid categoriaId, string nombre)
    {
        if (categoriaId == Guid.Empty) throw new DomainException("La subcategoría requiere una categoría.");
        if (string.IsNullOrWhiteSpace(nombre)) throw new DomainException("La subcategoría debe tener nombre.");
        return new Subcategoria { CategoriaId = categoriaId, Nombre = nombre.Trim(), Activa = true };
    }

    public void Actualizar(string nombre, bool activa)
    {
        if (string.IsNullOrWhiteSpace(nombre)) throw new DomainException("La subcategoría debe tener nombre.");
        Nombre = nombre.Trim();
        Activa = activa;
    }
}
