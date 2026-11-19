namespace Application.DTOs;

public sealed record SubcategoriaDto(Guid Id, Guid CategoriaId, string Nombre, bool Activa);
public sealed record CrearCategoriaRequest(int Numero, string Nombre, int? Orden);
public sealed record ActualizarCategoriaRequest(string Nombre, bool Activa, int? Orden);
public sealed record CrearSubcategoriaRequest(Guid CategoriaId, string Nombre);
public sealed record ActualizarSubcategoriaRequest(string Nombre, bool Activa);
public sealed record CrearResenaRequest(int Calificacion, string? Comentario);
