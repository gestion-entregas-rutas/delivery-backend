
namespace Application.DTOs;

/// <param name="ZonaId">Opcional: si es null se arman las rutas de todas las zonas activas.</param>
public sealed record ArmarRutasRequest(DateOnly Fecha, Guid? ZonaId = null);
