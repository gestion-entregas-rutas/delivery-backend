namespace Application.DTOs;

/// <summary>Posición GPS de la ruta propia en curso. CapturadaEn debe enviarse en ISO 8601 con offset.</summary>
public sealed record RegistrarUbicacionRequest(Guid RutaId, double Latitud, double Longitud,
    double PrecisionMetros,
    [property: System.Text.Json.Serialization.JsonConverter(typeof(Application.Common.FechaConOffsetConverter))]
    DateTimeOffset CapturadaEn);
