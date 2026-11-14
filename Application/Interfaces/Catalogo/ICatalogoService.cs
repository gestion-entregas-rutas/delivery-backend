using Application.DTOs;

namespace Application.Interfaces;

public interface ICatalogoService
{
    Task<IReadOnlyList<CategoriaDto>> ListarCategoriasAsync(CancellationToken ct);
    Task<IReadOnlyList<SubcategoriaDto>> ListarSubcategoriasAsync(Guid? categoriaId, bool incluirInactivas, CancellationToken ct);
    Task<PaginaDto<ProductoResumenDto>> ListarProductosAsync(Guid? categoriaId, Guid? subcategoriaId, string? buscar, bool soloOfertas, decimal? precioMin, decimal? precioMax, string? orden, int pagina, int tamanoPagina, bool incluirInactivos, CancellationToken ct);
    Task<ProductoDetalleDto> ObtenerProductoAsync(Guid id, bool incluirInactivos, CancellationToken ct);
    Task<ProductoDetalleDto> CrearProductoAsync(ProductoUpsertRequest request, CancellationToken ct);
    Task<ProductoDetalleDto> ActualizarProductoAsync(Guid id, ProductoUpsertRequest request, CancellationToken ct);
    Task<ProductoDetalleDto> AjustarStockAsync(Guid id, int stock, CancellationToken ct);
    Task DesactivarProductoAsync(Guid id, CancellationToken ct);
    Task<ProductoDetalleDto> ReactivarProductoAsync(Guid id, CancellationToken ct);
    Task<CategoriaDto> CrearCategoriaAsync(CrearCategoriaRequest request, CancellationToken ct);
    Task<CategoriaDto> ActualizarCategoriaAsync(Guid id, ActualizarCategoriaRequest request, CancellationToken ct);
    Task<SubcategoriaDto> CrearSubcategoriaAsync(CrearSubcategoriaRequest request, CancellationToken ct);
    Task<SubcategoriaDto> ActualizarSubcategoriaAsync(Guid id, ActualizarSubcategoriaRequest request, CancellationToken ct);
    Task<ResenaDto> CrearResenaAsync(Guid productoId, Guid clienteId, CrearResenaRequest request, CancellationToken ct);
    Task SembrarCatalogoInicialAsync(CancellationToken ct);
}
