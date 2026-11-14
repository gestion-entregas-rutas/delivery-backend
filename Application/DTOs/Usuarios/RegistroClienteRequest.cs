namespace Application.DTOs;

public sealed record RegistroClienteRequest(string NombreCompleto, string Correo, string? Telefono, string Password, string? Cedula = null);
