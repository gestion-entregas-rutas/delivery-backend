namespace Application.DTOs;
public sealed record CambiarCredencialesRequest(string ClaveActual, string NuevoNombreUsuario, string NuevaClave);
