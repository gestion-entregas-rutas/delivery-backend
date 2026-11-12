using Domain.Common;
using Domain.Exceptions;
using Domain.ValueObjects;

namespace Domain.Entities;

public class Zona : Entity
{
    public const int MaxPedidosPorRutaPorDefecto = 4;

    private readonly List<GeoPoint> _poligono = [];

    private Zona() { } // EF Core

    public string Nombre { get; private set; } = null!;
    public bool Activa { get; private set; }
    public int MaxPedidosPorRuta { get; private set; }
    public IReadOnlyList<GeoPoint> Poligono => _poligono.AsReadOnly();

    public static Zona Crear(string nombre, IEnumerable<GeoPoint> poligono,
        int maxPedidosPorRuta = MaxPedidosPorRutaPorDefecto)
    {
        var zona = new Zona { Activa = true };
        zona.Actualizar(nombre, poligono, maxPedidosPorRuta);
        return zona;
    }

    public void Actualizar(string nombre, IEnumerable<GeoPoint> poligono, int maxPedidosPorRuta)
    {
        if (string.IsNullOrWhiteSpace(nombre))
            throw new DomainException("La zona debe tener un nombre.");
        if (maxPedidosPorRuta < 1)
            throw new DomainException("El máximo de pedidos por ruta debe ser al menos 1.");

        var vertices = poligono.ToList();
        if (vertices.Count < 3)
            throw new DomainException("El polígono de una zona necesita al menos 3 vértices.");

        Nombre = nombre.Trim();
        MaxPedidosPorRuta = maxPedidosPorRuta;
        _poligono.Clear();
        _poligono.AddRange(vertices);
    }

    public void Activar() => Activa = true;

    public void Desactivar() => Activa = false;

    public bool Contiene(GeoPoint punto)
    {
        var dentro = false;
        for (int i = 0, j = _poligono.Count - 1; i < _poligono.Count; j = i++)
        {
            var a = _poligono[i];
            var b = _poligono[j];
            var cruza = (a.Latitud > punto.Latitud) != (b.Latitud > punto.Latitud) &&
                        punto.Longitud < (b.Longitud - a.Longitud) * (punto.Latitud - a.Latitud) /
                            (b.Latitud - a.Latitud) + a.Longitud;
            if (cruza) dentro = !dentro;
        }
        return dentro;
    }
}
