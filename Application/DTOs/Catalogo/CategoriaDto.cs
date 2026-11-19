namespace Application.DTOs;

public sealed record CategoriaDto(Guid Id, int Numero, string Nombre, bool Activa, int CantidadProductos);
