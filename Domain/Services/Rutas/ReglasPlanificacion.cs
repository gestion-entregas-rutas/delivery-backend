using Domain.Exceptions;
using Domain.ValueObjects;

namespace Domain.Services;

/// <summary>Parámetros iniciales del modelo aproximado; deben calibrarse con entregas reales.</summary>
public sealed class ReglasPlanificacion
{
    public string ZonaHoraria { get; set; } = "America/Guayaquil";
    public TimeOnly InicioJornada { get; set; } = new(8, 0);
    public TimeOnly? FinJornada { get; set; }
    public int DuracionIntervaloMinutos { get; set; } = 30;
    public int TiempoServicioMinutos { get; set; } = 5;
    public int MargenTrasladoMinutos { get; set; } = 5;
    public double VelocidadPromedioKmh { get; set; } = 20;
    public double FactorDistancia { get; set; } = 1.3;

    /// <summary>
    /// Último instante que se ofrece a clientes cuando <see cref="FinJornada"/> no está definido.
    /// Solo limita la oferta de intervalos; no se usa como cierre operativo de las rutas.
    /// </summary>
    public TimeOnly? FinOfertaIntervalos { get; set; }

    /// <summary>Coordenadas del local (salida de rutas nuevas al ofrecer intervalos). Opcionales.</summary>
    public double? OrigenLatitud { get; set; }
    public double? OrigenLongitud { get; set; }

    /// <summary>Días hacia adelante que se pueden consultar o reservar (0 = solo hoy).</summary>
    public int DiasMaximosOferta { get; set; } = 30;

    public GeoPoint? OrigenLocal => OrigenLatitud is { } lat && OrigenLongitud is { } lon ? new GeoPoint(lat, lon) : null;

    public void Validar()
    {
        if (DuracionIntervaloMinutos is < 1 or > 1440 || TiempoServicioMinutos is < 1 or > 240 ||
            MargenTrasladoMinutos is < 0 or > 120 || !double.IsFinite(VelocidadPromedioKmh) ||
            VelocidadPromedioKmh is <= 0 or > 150 || !double.IsFinite(FactorDistancia) ||
            FactorDistancia is < 1 or > 10 || FinJornada <= InicioJornada)
            throw new DomainException("Parámetros de planificación inválidos; la jornada debe terminar después de iniciar.");
        if (FinOfertaIntervalos is { } finOferta && (finOferta <= InicioJornada || FinJornada is { } fin && finOferta > fin))
            throw new DomainException("FinOfertaIntervalos debe estar después del inicio y no después del fin de jornada.");
        if (OrigenLatitud.HasValue != OrigenLongitud.HasValue ||
            OrigenLatitud is { } la && (!double.IsFinite(la) || la is < -90 or > 90) ||
            OrigenLongitud is { } lo && (!double.IsFinite(lo) || lo is < -180 or > 180))
            throw new DomainException("Indique latitud y longitud del local juntas y dentro de rango.");
        if (DiasMaximosOferta is < 0 or > 365)
            throw new DomainException("DiasMaximosOferta debe estar entre 0 y 365.");
        try { _ = TimeZoneInfo.FindSystemTimeZoneById(ZonaHoraria); }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException or ArgumentException)
        { throw new DomainException("Zona horaria de planificación inválida."); }
    }

    public DateTimeOffset Instante(DateOnly fecha, TimeOnly hora)
    {
        var zona = TimeZoneInfo.FindSystemTimeZoneById(ZonaHoraria);
        var local = fecha.ToDateTime(hora, DateTimeKind.Unspecified);
        if (zona.IsInvalidTime(local) || zona.IsAmbiguousTime(local))
            throw new DomainException("La hora local es inválida o ambigua en la zona horaria configurada.");
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, zona));
    }

    /// <summary>Fecha local del negocio para un instante.</summary>
    public DateOnly FechaLocal(DateTimeOffset instante) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instante, TimeZoneInfo.FindSystemTimeZoneById(ZonaHoraria)).DateTime);

    public TimeOnly HoraLocal(DateTimeOffset instante) =>
        TimeOnly.FromDateTime(TimeZoneInfo.ConvertTime(instante, TimeZoneInfo.FindSystemTimeZoneById(ZonaHoraria)).DateTime);

    public ContextoPlanificacion CrearContexto(DateOnly fecha, GeoPoint origen,
        DateTimeOffset ahora, DateTimeOffset? salidaSolicitada = null)
    {
        Validar();
        var apertura = Instante(fecha, InicioJornada);
        var salida = salidaSolicitada?.ToUniversalTime() ?? (ahora > apertura ? ahora.ToUniversalTime() : apertura);
        if (salida < apertura || salida < ahora || FechaLocal(salida) != fecha)
            throw new DomainException("La salida debe pertenecer a la fecha solicitada, dentro de la jornada y no estar en el pasado.");
        var cierre = FinJornada is { } fin ? Instante(fecha, fin) : (DateTimeOffset?)null;
        if (cierre is { } limite && salida >= limite)
            throw new DomainException("La salida debe ser anterior al fin de jornada.");
        return new ContextoPlanificacion(origen, salida, cierre, this);
    }

    /// <summary>
    /// Contexto para recalcular una ruta ya existente o en curso: parte de la posición y el instante
    /// reales indicados, sin exigir que estén dentro de la jornada (el repartidor pudo salir antes).
    /// </summary>
    public ContextoPlanificacion ContextoOperativo(DateOnly fecha, GeoPoint posicion, DateTimeOffset salida)
    {
        Validar();
        var cierre = FinJornada is { } fin ? Instante(fecha, fin) : (DateTimeOffset?)null;
        return new ContextoPlanificacion(posicion, salida.ToUniversalTime(), cierre, this);
    }

    /// <summary>Límite de la oferta: fin de jornada si existe; si no, el fin configurado para la oferta.</summary>
    public DateTimeOffset? CierreOferta(DateOnly fecha) =>
        FinJornada is { } fin ? Instante(fecha, fin) :
        FinOfertaIntervalos is { } oferta ? Instante(fecha, oferta) : null;

    /// <summary>Rejilla de intervalos del día, desde la apertura y de duración fija, que termina antes del cierre.</summary>
    public IReadOnlyList<(DateTimeOffset Inicio, DateTimeOffset Fin)> IntervalosDelDia(DateOnly fecha)
    {
        Validar();
        var cierre = CierreOferta(fecha)
            ?? throw new DomainException("Configure FinJornada o FinOfertaIntervalos para ofrecer intervalos de entrega.");
        var resultado = new List<(DateTimeOffset, DateTimeOffset)>();
        for (var inicio = Instante(fecha, InicioJornada); inicio.AddMinutes(DuracionIntervaloMinutos) <= cierre;
             inicio = inicio.AddMinutes(DuracionIntervaloMinutos))
            resultado.Add((inicio, inicio.AddMinutes(DuracionIntervaloMinutos)));
        return resultado;
    }

    /// <summary>Solo se aceptan intervalos exactamente iguales a uno de la rejilla ofrecida.</summary>
    public bool EsIntervaloOfertado(DateOnly fecha, DateTimeOffset inicio, DateTimeOffset fin) =>
        IntervalosDelDia(fecha).Any(i => i.Inicio == inicio.ToUniversalTime() && i.Fin == fin.ToUniversalTime());
}

public sealed record ContextoPlanificacion(GeoPoint Origen, DateTimeOffset Salida,
    DateTimeOffset? FinJornada, ReglasPlanificacion Reglas);
