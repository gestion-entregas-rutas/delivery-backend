namespace Application.DTOs;

public sealed record ProductoMediaDto(string Tipo, string Url, int Orden);
public sealed record ProductoFichaDto(string Clave, string Valor);
public sealed record ResenaDto(Guid Id, Guid ClienteId, int Calificacion, string? Comentario, DateTimeOffset CreadaEn);

public sealed record ProductoDetalleDto(
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
    IReadOnlyList<ProductoMediaDto> GaleriaMultimedia,
    IReadOnlyList<ProductoFichaDto> FichaTecnica,
    IReadOnlyList<string> SobreEsteArticulo,
    double CalificacionPromedio,
    int TotalResenas,
    string Version,
    IReadOnlyList<ResenaDto> Resenas);
