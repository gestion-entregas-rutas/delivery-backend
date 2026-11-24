using Application.DTOs;
using Domain.Entities;

namespace Application.Mappings;

public static class RutaMapeos
{
    private static readonly HashSet<string> EstadosAlerta = ["RetrasoPrevisto", "PlazoVencido"];

    public static RutaDto ToDto(this Ruta ruta, DateTimeOffset? ahora = null)
    {
        var paradas = ruta.Paradas.Select(p => new ParadaDto(p.Orden,
            p.Pedido.ToDto(ahora, p.FinalizacionEstimadaEn), p.LlegadaEstimadaEn,
            p.InicioAtencionEstimadoEn, p.FinalizacionEstimadaEn, p.TrasladoEstimadoMinutos)).ToList();
        // Alertas calculadas al consultar (no persistidas): entregas con retraso previsto o plazo vencido.
        var alertas = paradas.Where(p => p.Pedido.Puntualidad is { } r && EstadosAlerta.Contains(r.Estado))
            .Select(p => new AlertaPuntualidadDto(p.Pedido.Id, p.Orden, p.Pedido.Puntualidad!.Estado,
                p.Pedido.Puntualidad.RetrasoMinutos)).ToList();
        return new(ruta.Id, ruta.Fecha, ruta.ZonaId, ruta.RepartidorId, ruta.Capacidad, ruta.Estado, paradas, ruta.Version,
            ruta.OrigenLatitud is { } lat && ruta.OrigenLongitud is { } lon ? new PuntoDto(lat, lon) : null,
            ruta.SalidaProgramadaEn, ruta.EstimacionesActualizadasEn, ruta.ModeloEstimacion, ruta.DistanciaEstimadaKm, alertas);
    }
}
