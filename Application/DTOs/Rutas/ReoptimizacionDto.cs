namespace Application.DTOs;

/// <summary>
/// Reoptimiza las rutas <c>Planificada</c> de una fecha (y zona): se deshacen y se vuelven a armar
/// junto con los pedidos pendientes. Las rutas en curso o completadas no se tocan.
/// </summary>
/// <param name="Aplicar">false = solo vista previa (no cambia nada); true = aplica en una transacción.</param>
public sealed record ReoptimizarRutasRequest(DateOnly Fecha, Guid? ZonaId = null, PuntoDto? Origen = null, bool Aplicar = false);

public sealed record ParadaPropuestaDto(Guid PedidoId, int Orden, string ClienteNombre, string Direccion,
    DateTimeOffset? CompromisoInicioEn, DateTimeOffset? CompromisoFinEn,
    DateTimeOffset? FinalizacionEstimadaEn, bool ExcedeCompromiso, bool CambiaDeRepartidor);

public sealed record RutaPropuestaDto(Guid ZonaId, Guid RepartidorId, string RepartidorNombre, int Capacidad,
    IReadOnlyList<ParadaPropuestaDto> Paradas, double? DistanciaEstimadaKm);

/// <param name="RutasReemplazadas">Rutas planificadas que se deshacen (o se desharían).</param>
/// <param name="PedidosQueCambianDeRepartidor">Pedidos que ya tenían ruta y pasan a otro repartidor.</param>
/// <param name="PedidosQuedanSinRuta">Pedidos que tenían ruta y en la propuesta quedan sin asignar.</param>
/// <param name="ParadasEnRiesgo">Paradas cuya finalización estimada excede su intervalo comprometido.</param>
/// <param name="Rutas">Rutas creadas (solo cuando se aplica).</param>
public sealed record ReoptimizacionDto(DateOnly Fecha, bool Aplicada, int RutasReemplazadas,
    IReadOnlyList<RutaPropuestaDto> Propuestas, int PedidosSinAsignar, int PedidosQueCambianDeRepartidor,
    int PedidosQuedanSinRuta, int ParadasEnRiesgo, IReadOnlyList<RutaDto> Rutas);
