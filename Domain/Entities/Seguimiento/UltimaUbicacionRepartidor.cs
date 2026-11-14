using Domain.Common;
using Domain.Exceptions;
using Domain.ValueObjects;
namespace Domain.Entities;

public sealed class UltimaUbicacionRepartidor : Entity
{
    private UltimaUbicacionRepartidor() { }
    public Guid RepartidorId { get; private set; }
    public Guid RutaId { get; private set; }
    public GeoPoint Punto { get; private set; }
    public double PrecisionMetros { get; private set; }
    public DateTimeOffset CapturadaEn { get; private set; }
    public DateTimeOffset RecibidaEn { get; private set; }

    public static UltimaUbicacionRepartidor Crear(Guid repartidorId) => new() { RepartidorId = repartidorId };
    public bool Actualizar(Guid rutaId, GeoPoint punto, double precision, DateTimeOffset capturada, DateTimeOffset recibida)
    {
        if (!double.IsFinite(precision) || precision is < 0 or > 1000)
            throw new DomainException("La precisión debe estar entre 0 y 1000 metros.");
        if (capturada > recibida.AddMinutes(1) || capturada < recibida.AddMinutes(-5))
            throw new DomainException("La captura debe estar dentro de los últimos cinco minutos y no exceder un minuto futuro.");
        if (capturada <= CapturadaEn) return false;
        RutaId = rutaId; Punto = punto; PrecisionMetros = precision;
        CapturadaEn = capturada.ToUniversalTime(); RecibidaEn = recibida.ToUniversalTime();
        return true;
    }
}
