namespace Application.DTOs;

/// <summary>Ubicación del local (punto de salida de las rutas).</summary>
public sealed record UbicacionLocalDto(string Nombre, string Direccion, double Latitud, double Longitud, DateTimeOffset ActualizadoEn);

/// <summary>Guarda (crea o reemplaza) la ubicación del local.</summary>
public sealed record GuardarUbicacionLocalRequest(string Nombre, string Direccion, double Latitud, double Longitud);
