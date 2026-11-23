namespace Domain.Services;

public sealed record ResultadoPuntualidad(string Estado, int? RetrasoMinutos);

public static class Puntualidad
{
    public static ResultadoPuntualidad Calcular(DateTimeOffset? inicio, DateTimeOffset? limite,
        DateTimeOffset? entregado, DateTimeOffset? finalizacionEstimada, DateTimeOffset ahora)
    {
        if (limite is null) return new("SinCompromiso", null);
        int Retraso(DateTimeOffset instante) => (int)Math.Ceiling(Math.Max(0, (instante - limite.Value).TotalMinutes));
        if (entregado is { } real)
            return new(real > limite ? "EntregadoConRetraso" : real < inicio ? "EntregadoAntesDelIntervalo" : "EntregadoEnIntervalo", Retraso(real));
        if (ahora > limite) return new("PlazoVencido", Retraso(ahora));
        if (finalizacionEstimada is not { } estimada) return new("SinEstimacion", null);
        return new(estimada > limite ? "RetrasoPrevisto" : "DentroDelPlazoPrevisto", Retraso(estimada));
    }
}
