using Domain.Entities;
using Domain.ValueObjects;

namespace Domain.Services;

/// <param name="ExcedeCompromiso">La finalización estimada supera el compromiso o el fin de jornada.</param>
public sealed record EstimacionParada(Guid PedidoId, DateTimeOffset LlegadaEn,
    DateTimeOffset InicioAtencionEn, DateTimeOffset FinalizacionEn, double TrasladoMinutos,
    bool ExcedeCompromiso = false);

public sealed record EstimacionRuta(ContextoPlanificacion Contexto,
    IReadOnlyList<EstimacionParada> Paradas, double DistanciaEstimadaKm)
{
    /// <summary>Pedidos cuya finalización estimada no cumple su compromiso o el cierre.</summary>
    public IReadOnlySet<Guid> Atrasadas => Paradas.Where(p => p.ExcedeCompromiso).Select(p => p.PedidoId).ToHashSet();
}

/// <summary>
/// Entrega a evaluar en el modelo de tiempos: puede ser un pedido real o una reserva de checkout
/// (todavía sin pedido). El compromiso es opcional para pedidos anteriores.
/// </summary>
public sealed record DemandaEntrega(Guid Id, GeoPoint Ubicacion, DateTimeOffset? Inicio,
    DateTimeOffset? Fin, int? ServicioMinutos)
{
    public static DemandaEntrega De(Pedido pedido) => new(pedido.Id, pedido.Ubicacion,
        pedido.CompromisoInicioEn, pedido.CompromisoFinEn, pedido.TiempoServicioMinutos);
}

/// <summary>Modelo Haversine ajustado por factor/velocidad. No representa tráfico ni calles reales.</summary>
public static class CalculadorTiemposRuta
{
    /// <summary>Estricto: null si alguna entrega excede su compromiso o el cierre (uso del planificador).</summary>
    public static EstimacionRuta? Evaluar(IReadOnlyList<Pedido> pedidos, ContextoPlanificacion contexto) =>
        Evaluar(pedidos.Select(DemandaEntrega.De).ToList(), contexto);

    public static EstimacionRuta? Evaluar(IReadOnlyList<DemandaEntrega> entregas, ContextoPlanificacion contexto)
    {
        var estimacion = Estimar(entregas, contexto);
        return estimacion.Paradas.Any(p => p.ExcedeCompromiso) ? null : estimacion;
    }

    /// <summary>
    /// Tolerante: siempre devuelve todas las paradas y marca las que exceden. Sirve para mostrar
    /// estimaciones operativas y detectar qué entregas quedan en riesgo tras un cambio.
    /// </summary>
    public static EstimacionRuta Estimar(IReadOnlyList<Pedido> pedidos, ContextoPlanificacion contexto) =>
        Estimar(pedidos.Select(DemandaEntrega.De).ToList(), contexto);

    public static EstimacionRuta Estimar(IReadOnlyList<DemandaEntrega> entregas, ContextoPlanificacion contexto)
    {
        contexto.Reglas.Validar();
        var posicion = contexto.Origen;
        var salida = contexto.Salida;
        double totalKm = 0;
        var paradas = new List<EstimacionParada>(entregas.Count);
        foreach (var entrega in entregas)
        {
            var km = posicion.DistanciaKm(entrega.Ubicacion) * contexto.Reglas.FactorDistancia;
            var minutos = km / contexto.Reglas.VelocidadPromedioKmh * 60 + contexto.Reglas.MargenTrasladoMinutos;
            var llegada = salida.AddMinutes(minutos);
            var inicio = entrega.Inicio is { } desde && desde > llegada ? desde : llegada;
            var fin = inicio.AddMinutes(entrega.ServicioMinutos ?? contexto.Reglas.TiempoServicioMinutos);
            var excede = entrega.Fin is { } hasta && fin > hasta ||
                         contexto.FinJornada is { } cierre && fin > cierre;
            paradas.Add(new(entrega.Id, llegada, inicio, fin, minutos, excede));
            salida = fin;
            posicion = entrega.Ubicacion;
            totalKm += km;
        }
        return new(contexto, paradas, totalKm);
    }

    /// <summary>
    /// Una secuencia modificada es aceptable si la entrega agregada (si hay) cumple y ninguna
    /// entrega que antes cumplía pasa a exceder. No exige corregir atrasos ya existentes.
    /// </summary>
    public static bool NoEmpeora(EstimacionRuta? antes, EstimacionRuta despues, Guid? agregada = null)
    {
        var previas = antes?.Atrasadas ?? new HashSet<Guid>();
        return despues.Paradas.All(p => !p.ExcedeCompromiso ||
            (p.PedidoId != agregada && previas.Contains(p.PedidoId)));
    }
}
