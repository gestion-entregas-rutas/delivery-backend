namespace Application.DTOs;

/// <summary>Totales de toda la población filtrada, independientes de la paginación. No representa SLA.</summary>
public sealed record ResumenPedidosDto(int Total, int NoCancelados, int Entregados, decimal PorcentajeEntregado,
    IReadOnlyList<ConteoEstadoDto> PorEstado, IReadOnlyList<ConteoZonaDto> PorZona, DateTimeOffset CalculadoEn);
