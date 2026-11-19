namespace Application.DTOs;

public sealed record ProductoMediaRequest(string Tipo, string Url, int Orden);
public sealed record ProductoFichaRequest(string Clave, string Valor);
public sealed record ProductoUpsertRequest(
    Guid CategoriaId,
    Guid? SubcategoriaId,
    string? Subcategoria,
    string Nombre,
    string? Marca,
    string? DescripcionCorta,
    decimal PrecioBase,
    decimal? PrecioOferta,
    int Stock,
    bool Activo,
    string? ImagenPrincipalUrl,
    List<ProductoMediaRequest>? GaleriaMultimedia,
    List<ProductoFichaRequest>? FichaTecnica,
    List<string>? SobreEsteArticulo,
    string? Version);
