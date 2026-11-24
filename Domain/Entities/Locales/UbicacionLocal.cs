using Domain.Common;
using Domain.Exceptions;

namespace Domain.Entities;

/// <summary>
/// Ubicación del local (punto de salida de las rutas). Es un registro único para todo el sistema:
/// la usan el armado de rutas, las estimaciones y la oferta de intervalos de la tienda.
/// </summary>
public sealed class UbicacionLocal : Entity
{
    /// <summary>Identificador fijo del único registro.</summary>
    public static readonly Guid IdUnico = Guid.Parse("0193f2a0-0000-7000-8000-000000000001");

    private UbicacionLocal() { }
    public string Nombre { get; private set; } = string.Empty;
    public string Direccion { get; private set; } = string.Empty;
    public double Latitud { get; private set; }
    public double Longitud { get; private set; }
    public DateTimeOffset ActualizadoEn { get; private set; }

    public static UbicacionLocal Crear(string nombre, string direccion, double latitud, double longitud, DateTimeOffset ahora)
    {
        var local = new UbicacionLocal { Id = IdUnico };
        local.Actualizar(nombre, direccion, latitud, longitud, ahora);
        return local;
    }

    public void Actualizar(string nombre, string direccion, double latitud, double longitud, DateTimeOffset ahora)
    {
        if (!double.IsFinite(latitud) || latitud is < -90 or > 90 || !double.IsFinite(longitud) || longitud is < -180 or > 180)
            throw new DomainException("La ubicación del local no es válida.");
        Nombre = string.IsNullOrWhiteSpace(nombre) ? "Nuestro local" : nombre.Trim()[..Math.Min(nombre.Trim().Length, 120)];
        Direccion = (direccion ?? string.Empty).Trim()[..Math.Min((direccion ?? string.Empty).Trim().Length, 300)];
        Latitud = latitud;
        Longitud = longitud;
        ActualizadoEn = ahora;
    }
}
