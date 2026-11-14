using Domain.Common;
using Domain.Exceptions;

namespace Domain.Entities;

public sealed class Categoria : Entity
{
    private Categoria() { }
    public int Numero { get; private set; }
    public string Nombre { get; private set; } = null!;
    public bool Activa { get; private set; } = true;
    public int Orden { get; private set; }

    public static Categoria Crear(int numero, string nombre, int orden)
    {
        if (string.IsNullOrWhiteSpace(nombre)) throw new DomainException("La categoría debe tener nombre.");
        return new Categoria { Numero = numero, Nombre = nombre.Trim(), Orden = orden, Activa = true };
    }

    public void Actualizar(string nombre, bool activa)
    {
        if (string.IsNullOrWhiteSpace(nombre)) throw new DomainException("La categoría debe tener nombre.");
        Nombre = nombre.Trim();
        Activa = activa;
    }
}
