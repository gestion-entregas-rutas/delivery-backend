using Domain.Entities;
using Domain.Enums;

namespace Domain.Services;

/// <summary>
/// Heurística de inserción más barata para pedidos de último momento: elige, entre las rutas
/// de la misma fecha y zona con espacio, la posición que menos kilómetros agrega.
/// Si devuelve null, el llamador debe crear una ruta nueva o dejar el pedido en espera.
/// Requiere las paradas con su pedido cargado.
/// </summary>
public static class InsercionRutas
{
    public static PropuestaInsercion? BuscarMejor(Pedido pedido, IEnumerable<Ruta> rutas)
    {
        PropuestaInsercion? mejor = null;

        foreach (var ruta in rutas)
        {
            if (ruta.Fecha != pedido.FechaEntrega || ruta.ZonaId != pedido.ZonaId) continue;
            if (!ruta.TieneEspacio || ruta.Estado is not (EstadoRuta.Planificada or EstadoRuta.EnCurso)) continue;

            for (var i = ruta.PosicionMinimaInsercion; i <= ruta.Paradas.Count; i++)
            {
                var costo = CostoInsertarEn(ruta, pedido, i);
                if (mejor is null || costo < mejor.Value.CostoAdicionalKm)
                    mejor = new PropuestaInsercion(ruta, i, costo);
            }
        }

        return mejor;
    }

    private static double CostoInsertarEn(Ruta ruta, Pedido pedido, int indice)
    {
        var paradas = ruta.Paradas;
        var p = pedido.Ubicacion;

        if (paradas.Count == 0) return 0;
        if (indice == 0) return p.DistanciaKm(paradas[0].Pedido.Ubicacion);
        if (indice == paradas.Count) return paradas[^1].Pedido.Ubicacion.DistanciaKm(p);

        var anterior = paradas[indice - 1].Pedido.Ubicacion;
        var siguiente = paradas[indice].Pedido.Ubicacion;
        return anterior.DistanciaKm(p) + p.DistanciaKm(siguiente) - anterior.DistanciaKm(siguiente);
    }
}
