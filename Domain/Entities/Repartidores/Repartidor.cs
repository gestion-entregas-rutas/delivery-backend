using Domain.Common;
using Domain.Enums;
using Domain.Exceptions;

namespace Domain.Entities;

public class Repartidor : Entity
{
    private Repartidor() { } // EF Core

    public string Nombre { get; private set; } = null!;
    public string Telefono { get; private set; } = null!;
    public int CapacidadMaxima { get; private set; }
    public EstadoRepartidor Estado { get; private set; }
    public bool EstaDisponible => Estado == EstadoRepartidor.Disponible;

    public static Repartidor Crear(string nombre, string telefono, int capacidadMaxima)
    {
        if (string.IsNullOrWhiteSpace(nombre))
            throw new DomainException("El repartidor debe tener un nombre.");
        if (string.IsNullOrWhiteSpace(telefono))
            throw new DomainException("El repartidor debe tener un teléfono.");
        if (capacidadMaxima < 1)
            throw new DomainException("La capacidad por salida debe ser al menos 1.");

        return new Repartidor
        {
            Nombre = nombre.Trim(),
            Telefono = telefono.Trim(),
            CapacidadMaxima = capacidadMaxima,
            Estado = EstadoRepartidor.Disponible
        };
    }

    public void MarcarDisponible()
    {
        if (Estado == EstadoRepartidor.EnRuta)
            throw new DomainException("Un repartidor en ruta no puede cambiar su disponibilidad.");
        Estado = EstadoRepartidor.Disponible;
    }

    public void MarcarNoDisponible()
    {
        if (Estado == EstadoRepartidor.EnRuta)
            throw new DomainException("Un repartidor en ruta no puede cambiar su disponibilidad.");
        Estado = EstadoRepartidor.NoDisponible;
    }

    public void IniciarSalida()
    {
        if (Estado != EstadoRepartidor.Disponible)
            throw new DomainException($"El repartidor no puede salir estando '{Estado}'.");
        Estado = EstadoRepartidor.EnRuta;
    }

    public void FinalizarSalida()
    {
        if (Estado != EstadoRepartidor.EnRuta)
            throw new DomainException("El repartidor no está en ruta.");
        Estado = EstadoRepartidor.Disponible;
    }
}
