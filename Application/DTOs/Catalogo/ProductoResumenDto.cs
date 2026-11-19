namespace Application.DTOs;

public sealed record ProductoResumenDto(
    Guid Id,
    Guid CategoriaId,
    string CategoriaNombre,
    string Subcategoria,
    string Nombre,
    string Marca,
    string DescripcionCorta,
    decimal PrecioBase,
    decimal? PrecioOferta,
    int Stock,
    bool Activo,
    string ImagenPrincipalUrl,
    double CalificacionPromedio,
    int TotalResenas,
    string Version);
