using Application.DTOs;
using Application.Exceptions;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Application.Services;

public sealed class CatalogoService(IAppDbContext db, TimeProvider reloj) : ICatalogoService
{
    private static string Ver(uint v) => Convert.ToBase64String(BitConverter.GetBytes(v));
    private static uint ParseVer(string? v)
    {
        if (string.IsNullOrWhiteSpace(v)) throw new ConflictoException("El producto fue editado por otro usuario. Recarga los datos antes de guardar.", "CONFLICTO_VERSION_PRODUCTO");
        try { return BitConverter.ToUInt32(Convert.FromBase64String(v), 0); }
        catch { throw new ConflictoException("El producto fue editado por otro usuario. Recarga los datos antes de guardar.", "CONFLICTO_VERSION_PRODUCTO"); }
    }

    public async Task<IReadOnlyList<CategoriaDto>> ListarCategoriasAsync(CancellationToken ct)
    {
        var catsActivasIds = db.Categorias.Where(c => c.Activa).Select(c => c.Id);
        var subsInactivasIds = db.Subcategorias.Where(s => !s.Activa).Select(s => s.Id);
        var conteos = await db.Productos.AsNoTracking()
            .Where(p => p.Activo
                && catsActivasIds.Contains(p.CategoriaId)
                && (p.SubcategoriaId == null || !subsInactivasIds.Contains(p.SubcategoriaId.Value)))
            .GroupBy(p => p.CategoriaId)
            .Select(g => new { CategoriaId = g.Key, Cantidad = g.Count() })
            .ToDictionaryAsync(x => x.CategoriaId, x => x.Cantidad, ct);
        var cats = await db.Categorias.AsNoTracking().Where(c => c.Activa).OrderBy(c => c.Orden).ThenBy(c => c.Numero).ToListAsync(ct);
        return cats.Select(c => new CategoriaDto(c.Id, c.Numero, c.Nombre, c.Activa, conteos.GetValueOrDefault(c.Id))).ToList();
    }

    public async Task<IReadOnlyList<SubcategoriaDto>> ListarSubcategoriasAsync(Guid? categoriaId, bool incluirInactivas, CancellationToken ct)
    {
        var q = db.Subcategorias.AsNoTracking().AsQueryable();
        if (categoriaId.HasValue) q = q.Where(s => s.CategoriaId == categoriaId.Value);
        if (!incluirInactivas)
        {
            var activas = db.Categorias.Where(c => c.Activa).Select(c => c.Id);
            q = q.Where(s => s.Activa && activas.Contains(s.CategoriaId));
        }
        return await q.OrderBy(s => s.Nombre).Select(s => new SubcategoriaDto(s.Id, s.CategoriaId, s.Nombre, s.Activa)).ToListAsync(ct);
    }

    public async Task<PaginaDto<ProductoResumenDto>> ListarProductosAsync(Guid? categoriaId, Guid? subcategoriaId, string? buscar, bool soloOfertas, decimal? precioMin, decimal? precioMax, string? orden, int pagina, int tamanoPagina, bool incluirInactivos, CancellationToken ct)
    {
        pagina = Math.Max(1, pagina);
        tamanoPagina = Math.Clamp(tamanoPagina <= 0 ? 20 : tamanoPagina, 1, 100);
        var q = db.Productos.AsNoTracking().AsQueryable();
        if (!incluirInactivos)
        {
            var catsActivas = db.Categorias.Where(c => c.Activa).Select(c => c.Id);
            var subsInactivas = db.Subcategorias.Where(s => !s.Activa).Select(s => s.Id);
            q = q.Where(p => p.Activo
                && catsActivas.Contains(p.CategoriaId)
                && (p.SubcategoriaId == null || !subsInactivas.Contains(p.SubcategoriaId.Value)));
        }
        if (categoriaId.HasValue) q = q.Where(p => p.CategoriaId == categoriaId.Value);
        if (subcategoriaId.HasValue) q = q.Where(p => p.SubcategoriaId == subcategoriaId.Value);
        if (soloOfertas) q = q.Where(p => p.PrecioOferta != null);
        if (!string.IsNullOrWhiteSpace(buscar))
        {
            var term = buscar.Trim();
            q = q.Where(p =>
                EF.Functions.Like(p.Nombre, $"%{term}%") ||
                EF.Functions.Like(p.Marca, $"%{term}%") ||
                EF.Functions.Like(p.DescripcionCorta, $"%{term}%") ||
                EF.Functions.Like(p.SubcategoriaTexto, $"%{term}%"));
        }
        if (precioMin.HasValue) q = q.Where(p => (p.PrecioOferta ?? p.PrecioBase) >= precioMin.Value);
        if (precioMax.HasValue) q = q.Where(p => (p.PrecioOferta ?? p.PrecioBase) <= precioMax.Value);

        q = (orden ?? "").ToLowerInvariant() switch
        {
            "precio_asc" => q.OrderBy(p => p.PrecioOferta ?? p.PrecioBase).ThenBy(p => p.Nombre),
            "precio_desc" => q.OrderByDescending(p => p.PrecioOferta ?? p.PrecioBase).ThenBy(p => p.Nombre),
            "calificacion" => q.OrderByDescending(p => p.CalificacionPromedio).ThenByDescending(p => p.TotalResenas),
            _ => q.OrderBy(p => p.Nombre)
        };

        var total = await q.CountAsync(ct);
        var productos = await q.Skip((pagina - 1) * tamanoPagina).Take(tamanoPagina).ToListAsync(ct);
        var catIds = productos.Select(p => p.CategoriaId).Distinct().ToList();
        var cats = await db.Categorias.AsNoTracking().Where(c => catIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Nombre, ct);
        var items = productos.Select(p => new ProductoResumenDto(p.Id, p.CategoriaId, cats.GetValueOrDefault(p.CategoriaId, ""), p.SubcategoriaTexto, p.Nombre, p.Marca, p.DescripcionCorta, p.PrecioBase, p.PrecioOferta, p.Stock, p.Activo, p.ImagenPrincipalUrl, p.CalificacionPromedio, p.TotalResenas, Ver(p.Version))).ToList();
        return new PaginaDto<ProductoResumenDto>(items, total, pagina, tamanoPagina);
    }

    public async Task<ProductoDetalleDto> ObtenerProductoAsync(Guid id, bool incluirInactivos, CancellationToken ct)
    {
        var p = await db.Productos.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NoEncontradoException(nameof(Producto), id);
        if (!incluirInactivos)
        {
            var catActiva = await db.Categorias.AsNoTracking().AnyAsync(c => c.Id == p.CategoriaId && c.Activa, ct);
            var subActiva = p.SubcategoriaId == null || await db.Subcategorias.AsNoTracking().AnyAsync(s => s.Id == p.SubcategoriaId && s.Activa, ct);
            if (!p.Activo || !catActiva || !subActiva) throw new NoEncontradoException(nameof(Producto), id);
        }
        var cat = await db.Categorias.AsNoTracking().FirstOrDefaultAsync(c => c.Id == p.CategoriaId, ct);
        var media = await db.ProductoMedias.AsNoTracking().Where(m => m.ProductoId == id).OrderBy(m => m.Orden).Select(m => new ProductoMediaDto(m.Tipo, m.Url, m.Orden)).ToListAsync(ct);
        var ficha = await db.ProductoFichasTecnicas.AsNoTracking().Where(f => f.ProductoId == id).OrderBy(f => f.Orden).Select(f => new ProductoFichaDto(f.Clave, f.Valor)).ToListAsync(ct);
        var sobre = await db.ProductoCaracteristicas.AsNoTracking().Where(c => c.ProductoId == id).OrderBy(c => c.Orden).Select(c => c.Texto).ToListAsync(ct);
        var resenas = await db.Resenas.AsNoTracking().Where(r => r.ProductoId == id).OrderByDescending(r => r.CreadaEn).Take(20)
            .Select(r => new ResenaDto(r.Id, r.ClienteId, r.Calificacion, r.Comentario, r.CreadaEn)).ToListAsync(ct);
        return new ProductoDetalleDto(p.Id, p.CategoriaId, cat?.Nombre ?? "", p.SubcategoriaTexto, p.Nombre, p.Marca, p.DescripcionCorta, p.PrecioBase, p.PrecioOferta, p.Stock, p.Activo, p.ImagenPrincipalUrl, media, ficha, sobre, p.CalificacionPromedio, p.TotalResenas, Ver(p.Version), resenas);
    }

    public async Task<ProductoDetalleDto> CrearProductoAsync(ProductoUpsertRequest request, CancellationToken ct)
    {
        ValidarUpsert(request, false);
        await ValidarCategoriaSubcategoriaActivas(request.CategoriaId, request.SubcategoriaId, ct);
        var sub = request.Subcategoria ?? "";
        if (request.SubcategoriaId.HasValue)
        {
            var sc = await db.Subcategorias.AsNoTracking().FirstOrDefaultAsync(s => s.Id == request.SubcategoriaId.Value, ct);
            if (sc is not null) sub = sc.Nombre;
        }
        var p = Producto.Crear(request.CategoriaId, request.SubcategoriaId, sub, request.Nombre, request.Marca, request.DescripcionCorta, request.PrecioBase, request.PrecioOferta, request.Stock, request.ImagenPrincipalUrl);
        if (!request.Activo) p.Desactivar();
        db.Productos.Add(p);
        await ReemplazarDetallesAsync(p.Id, request, ct, true);
        await db.SaveChangesAsync(ct);
        return await ObtenerProductoAsync(p.Id, true, ct);
    }

    public async Task<ProductoDetalleDto> ActualizarProductoAsync(Guid id, ProductoUpsertRequest request, CancellationToken ct)
    {
        ValidarUpsert(request, true);
        var p = await db.Productos.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NoEncontradoException(nameof(Producto), id);
        if (p.Version != ParseVer(request.Version))
            throw new ConflictoException("El producto fue editado por otro usuario. Recarga los datos antes de guardar.", "CONFLICTO_VERSION_PRODUCTO");
        await ValidarCategoriaSubcategoriaActivas(request.CategoriaId, request.SubcategoriaId, ct);
        var sub = request.Subcategoria ?? p.SubcategoriaTexto;
        if (request.SubcategoriaId.HasValue)
        {
            var sc = await db.Subcategorias.AsNoTracking().FirstOrDefaultAsync(s => s.Id == request.SubcategoriaId.Value, ct);
            if (sc is not null) sub = sc.Nombre;
        }
        p.Actualizar(request.CategoriaId, request.SubcategoriaId, sub, request.Nombre, request.Marca, request.DescripcionCorta, request.PrecioBase, request.PrecioOferta, request.ImagenPrincipalUrl, request.Activo);
        p.AjustarStock(request.Stock);
        await ReemplazarDetallesAsync(p.Id, request, ct, false);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { db.LimpiarCambios(); throw new ConflictoException("El producto fue editado por otro usuario. Recarga los datos antes de guardar.", "CONFLICTO_VERSION_PRODUCTO"); }
        return await ObtenerProductoAsync(p.Id, true, ct);
    }

    public async Task<ProductoDetalleDto> AjustarStockAsync(Guid id, int stock, CancellationToken ct)
    {
        var p = await db.Productos.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NoEncontradoException(nameof(Producto), id);
        p.AjustarStock(stock);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { db.LimpiarCambios(); throw new ConflictoException("El producto cambió. Intente nuevamente.", "CONFLICTO_VERSION_PRODUCTO"); }
        return await ObtenerProductoAsync(id, true, ct);
    }

    public async Task DesactivarProductoAsync(Guid id, CancellationToken ct)
    {
        var p = await db.Productos.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NoEncontradoException(nameof(Producto), id);
        p.Desactivar();
        await db.SaveChangesAsync(ct);
    }

    public async Task<ProductoDetalleDto> ReactivarProductoAsync(Guid id, CancellationToken ct)
    {
        var p = await db.Productos.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NoEncontradoException(nameof(Producto), id);
        var cat = await db.Categorias.AsNoTracking().FirstOrDefaultAsync(c => c.Id == p.CategoriaId, ct) ?? throw new DomainException("La categoría del producto no existe.");
        if (!cat.Activa) throw new DomainException("No se puede reactivar el producto porque su categoría está desactivada.");
        if (p.SubcategoriaId.HasValue)
        {
            var sub = await db.Subcategorias.AsNoTracking().FirstOrDefaultAsync(s => s.Id == p.SubcategoriaId.Value, ct);
            if (sub is not null && !sub.Activa) throw new DomainException("No se puede reactivar el producto porque su subcategoría está desactivada.");
        }
        p.Activar();
        await db.SaveChangesAsync(ct);
        return await ObtenerProductoAsync(id, true, ct);
    }

    public async Task<CategoriaDto> CrearCategoriaAsync(CrearCategoriaRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Nombre)) throw new DomainException("El nombre de la categoría es obligatorio.");
        if (await db.Categorias.AnyAsync(c => c.Numero == request.Numero, ct)) throw new DomainException("Ya existe una categoría con ese número.");
        var c = Categoria.Crear(request.Numero, request.Nombre, request.Orden ?? request.Numero);
        db.Categorias.Add(c);
        await db.SaveChangesAsync(ct);
        return new CategoriaDto(c.Id, c.Numero, c.Nombre, c.Activa, 0);
    }

    public async Task<CategoriaDto> ActualizarCategoriaAsync(Guid id, ActualizarCategoriaRequest request, CancellationToken ct)
    {
        var c = await db.Categorias.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NoEncontradoException(nameof(Categoria), id);
        c.Actualizar(request.Nombre, request.Activa);
        if (request.Orden.HasValue) typeof(Categoria).GetProperty("Orden")!.SetValue(c, request.Orden.Value);
        if (!request.Activa)
        {
            var subs = await db.Subcategorias.Where(s => s.CategoriaId == id).ToListAsync(ct);
            foreach (var s in subs) s.Actualizar(s.Nombre, false);
            var prods = await db.Productos.Where(p => p.CategoriaId == id).ToListAsync(ct);
            foreach (var p in prods) p.Desactivar();
        }
        await db.SaveChangesAsync(ct);
        var conteo = await db.Productos.CountAsync(p => p.CategoriaId == id && p.Activo, ct);
        return new CategoriaDto(c.Id, c.Numero, c.Nombre, c.Activa, conteo);
    }

    public async Task<SubcategoriaDto> CrearSubcategoriaAsync(CrearSubcategoriaRequest request, CancellationToken ct)
    {
        var cat = await db.Categorias.AsNoTracking().FirstOrDefaultAsync(c => c.Id == request.CategoriaId, ct) ?? throw new NoEncontradoException(nameof(Categoria), request.CategoriaId);
        if (!cat.Activa) throw new DomainException("No se puede crear subcategoría en una categoría desactivada.");
        if (await db.Subcategorias.AnyAsync(s => s.CategoriaId == request.CategoriaId && s.Nombre == request.Nombre.Trim(), ct))
            throw new DomainException("Ya existe una subcategoría con ese nombre en la categoría.");
        var s = Subcategoria.Crear(request.CategoriaId, request.Nombre);
        db.Subcategorias.Add(s);
        await db.SaveChangesAsync(ct);
        return new SubcategoriaDto(s.Id, s.CategoriaId, s.Nombre, s.Activa);
    }

    public async Task<SubcategoriaDto> ActualizarSubcategoriaAsync(Guid id, ActualizarSubcategoriaRequest request, CancellationToken ct)
    {
        var s = await db.Subcategorias.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NoEncontradoException(nameof(Subcategoria), id);
        s.Actualizar(request.Nombre, request.Activa);
        if (!request.Activa)
        {
            var prods = await db.Productos.Where(p => p.SubcategoriaId == id).ToListAsync(ct);
            foreach (var p in prods) p.Desactivar();
        }
        await db.SaveChangesAsync(ct);
        return new SubcategoriaDto(s.Id, s.CategoriaId, s.Nombre, s.Activa);
    }

    public async Task<ResenaDto> CrearResenaAsync(Guid productoId, Guid clienteId, CrearResenaRequest request, CancellationToken ct)
    {
        if (request.Calificacion is < 1 or > 5) throw new DomainException("La calificación debe estar entre 1 y 5.");
        var producto = await db.Productos.FirstOrDefaultAsync(p => p.Id == productoId, ct) ?? throw new NoEncontradoException(nameof(Producto), productoId);
        if (!producto.Activo) throw new DomainException("No se puede reseñar un producto inactivo.");
        var compro = await db.DetalleOrdenes.AnyAsync(d => d.ProductoId == productoId && db.Ordenes.Any(o => o.Id == d.OrdenId && o.ClienteId == clienteId && (o.Estado == EstadoOrden.Pagada || o.Estado == EstadoOrden.Entregada || o.Estado == EstadoOrden.Retirada)), ct);
        if (!compro) throw new DomainException("Solo puedes reseñar productos que hayas comprado.");
        if (await db.Resenas.AnyAsync(r => r.ProductoId == productoId && r.ClienteId == clienteId, ct))
            throw new DomainException("Ya registraste una reseña para este producto.");
        var resena = Resena.Crear(productoId, clienteId, request.Calificacion, request.Comentario, reloj.GetUtcNow());
        db.Resenas.Add(resena);
        await db.SaveChangesAsync(ct);
        var stats = await db.Resenas.Where(r => r.ProductoId == productoId).GroupBy(_ => 1)
            .Select(g => new { Prom = g.Average(x => x.Calificacion), Total = g.Count() }).FirstAsync(ct);
        producto.ActualizarCalificacion(stats.Prom, stats.Total);
        await db.SaveChangesAsync(ct);
        return new ResenaDto(resena.Id, resena.ClienteId, resena.Calificacion, resena.Comentario, resena.CreadaEn);
    }

    private async Task ValidarCategoriaSubcategoriaActivas(Guid categoriaId, Guid? subcategoriaId, CancellationToken ct)
    {
        var cat = await db.Categorias.AsNoTracking().FirstOrDefaultAsync(c => c.Id == categoriaId, ct) ?? throw new NoEncontradoException(nameof(Categoria), categoriaId);
        if (!cat.Activa) throw new DomainException("La categoría seleccionada está desactivada.");
        if (subcategoriaId.HasValue)
        {
            var sub = await db.Subcategorias.AsNoTracking().FirstOrDefaultAsync(s => s.Id == subcategoriaId.Value && s.CategoriaId == categoriaId, ct);
            if (sub is null) throw new DomainException("La subcategoría no pertenece a la categoría seleccionada.");
            if (!sub.Activa) throw new DomainException("La subcategoría seleccionada está desactivada.");
        }
    }

    private static void ValidarUpsert(ProductoUpsertRequest r, bool requiereVersion)
    {
        if (requiereVersion && string.IsNullOrWhiteSpace(r.Version))
            throw new ConflictoException("El producto fue editado por otro usuario. Recarga los datos antes de guardar.", "CONFLICTO_VERSION_PRODUCTO");
        if (string.IsNullOrWhiteSpace(r.Nombre)) throw new DomainException("El nombre es obligatorio.");
    }

    private async Task ReemplazarDetallesAsync(Guid productoId, ProductoUpsertRequest request, CancellationToken ct, bool nuevo)
    {
        if (!nuevo)
        {
            var medias = await db.ProductoMedias.Where(x => x.ProductoId == productoId).ToListAsync(ct);
            var fichas = await db.ProductoFichasTecnicas.Where(x => x.ProductoId == productoId).ToListAsync(ct);
            var cars = await db.ProductoCaracteristicas.Where(x => x.ProductoId == productoId).ToListAsync(ct);
            db.ProductoMedias.RemoveRange(medias); db.ProductoFichasTecnicas.RemoveRange(fichas); db.ProductoCaracteristicas.RemoveRange(cars);
        }
        foreach (var m in (request.GaleriaMultimedia ?? []).Where(x => !string.IsNullOrWhiteSpace(x.Url)))
            db.ProductoMedias.Add(ProductoMedia.Crear(productoId, m.Tipo, m.Url, m.Orden));
        var ordenFicha = 0;
        foreach (var f in (request.FichaTecnica ?? []).Where(x => !string.IsNullOrWhiteSpace(x.Clave)))
            db.ProductoFichasTecnicas.Add(ProductoFichaTecnica.Crear(productoId, f.Clave, f.Valor, ordenFicha++));
        var i = 0;
        foreach (var t in (request.SobreEsteArticulo ?? []).Where(x => !string.IsNullOrWhiteSpace(x)))
            db.ProductoCaracteristicas.Add(ProductoCaracteristica.Crear(productoId, t, i++));
    }

    public async Task SembrarCatalogoInicialAsync(CancellationToken ct)
    {
        if (await db.Categorias.AnyAsync(ct)) return;
        var nombres = new[]
        {
            "Todos los departamentos","Arte y artesanías","Automotriz","Bebé","Belleza y cuidado personal","Cine y TV","Computadoras","Deportes y actividades al aire libre","Electrónicos","Equipaje","Herramientas y mejoramiento del hogar","Hogar y cocina","Industria y Ciencia","Insumos para mascotas","Juguetes y juegos","Libros","Moda para Hombre","Moda para Mujer","Moda para Niñas","Moda para Niños","Música MP3","Música, CDs y Vinilos","Ofertas","Prime Video","Salud y Hogar","Software","Tienda Kindle","Videojuegos"
        };
        var categorias = new List<Categoria>();
        for (int idx = 0; idx < nombres.Length; idx++) categorias.Add(Categoria.Crear(idx, nombres[idx], idx));
        await db.Categorias.AddRangeAsync(categorias, ct);
        await db.SaveChangesAsync(ct);
        if (await db.Productos.AnyAsync(ct)) return;
        Categoria Cat(string n) => categorias.First(c => c.Nombre == n);
        var productos = new (string cat, string sub, string nombre, string marca, string desc, decimal precio, decimal? oferta, int stock, string img, (string k,string v)[] ficha, string[] bullets)[]
        {
            ("Hogar y cocina","Velas y Portavelas","Vela perfumada de manzana y canela azucarada, tarro grande","Yankee Candle","Hasta 150 horas de tiempo de combustión, fragancia de relleno de habitación.",19.99m,14.99m,100,"https://images.unsplash.com/photo-1603006905003-be475563bc59?w=800", new[]{("Marca","Yankee Candle"),("Dimensiones","4\" an. x 6.8\" al. pulgadas"),("Peso","2.52 libras"),("Material","Cera vegetal y mecha de fibra natural"),("Cantidad por paquete","1")}, new[]{"Manzana de canela azucarada: delicioso aroma de rodajas de manzana crujientes.","Vela de larga duración con hasta 150 horas de fragancia."}),
            ("Electrónicos","Audífonos","Audífonos inalámbricos con cancelación de ruido","SoundMax","Audio inmersivo y batería de larga duración.",89.99m,69.99m,50,"https://images.unsplash.com/photo-1518441902117-f0a5d49d2fbe?w=800", new[]{("Marca","SoundMax"),("Conectividad","Bluetooth 5.3"),("Batería","30 horas"),("Peso","250 g")}, new[]{"Cancelación activa de ruido.","Modo transparencia para llamadas."}),
            ("Computadoras","Accesorios","Mouse ergonómico inalámbrico","LogiPro","Precisión y comodidad para trabajo diario.",29.99m,null,80,"https://images.unsplash.com/photo-1527864550417-7fd91fc51a46?w=800", new[]{("Marca","LogiPro"),("DPI","1600"),("Conexión","USB / Bluetooth")}, new[]{"Diseño ergonómico.","Batería recargable."}),
            ("Deportes y actividades al aire libre","Hidratación","Botella térmica de acero inoxidable 1L","HydraGo","Mantiene bebidas frías y calientes.",24.50m,19.90m,120,"https://images.unsplash.com/photo-1602143407151-7111542de6e8?w=800", new[]{("Capacidad","1 litro"),("Material","Acero inoxidable")}, new[]{"Aislamiento térmico doble pared.","Libre de BPA."}),
            ("Belleza y cuidado personal","Cuidado de la piel","Crema hidratante facial diaria","DermaCare","Hidratación ligera para uso diario.",15.99m,12.49m,90,"https://images.unsplash.com/photo-1556228720-195a672e8a03?w=800", new[]{("Tipo","Hidratante"),("Contenido","50 ml")}, new[]{"Absorción rápida.","Dermatológicamente probada."}),
            ("Hogar y cocina","Cocina","Sartén antiadherente 28 cm","ChefHome","Cocción uniforme y fácil limpieza.",34.99m,27.99m,60,"https://images.unsplash.com/photo-1556909114-f6e7ad7d3136?w=800", new[]{("Diámetro","28 cm"),("Material","Aluminio")}, new[]{"Recubrimiento antiadherente.","Mango ergonómico."}),
            ("Libros","Negocios","Hábitos Atómicos (Edición en español)","Editorial Planeta","Guía práctica para mejorar cada día.",21.00m,17.50m,70,"https://images.unsplash.com/photo-1544947950-fa07a98d237f?w=800", new[]{("Formato","Tapa blanda"),("Idioma","Español")}, new[]{"Estrategias basadas en evidencia.","Fácil de aplicar."}),
            ("Juguetes y juegos","Didácticos","Bloques de construcción creativos 500 piezas","BuildFun","Estimula creatividad y motricidad.",39.99m,32.00m,40,"https://images.unsplash.com/photo-1587654780291-39c9404d746b?w=800", new[]{("Piezas","500"),("Edad recomendada","6+")}, new[]{"Compatibles con sets estándar.","Colores variados."}),
            ("Insumos para mascotas","Perros","Cama acolchada para perro mediano","PetComfort","Descanso cómodo y lavable.",44.99m,36.99m,35,"https://images.unsplash.com/photo-1541599540903-216a46ca1dc0?w=800", new[]{("Talla","Mediana"),("Material","Poliéster")}, new[]{"Funda removible.","Base antideslizante."}),
            ("Herramientas y mejoramiento del hogar","Herramientas manuales","Juego de destornilladores 12 piezas","FixIt","Kit esencial para el hogar.",18.99m,null,75,"https://images.unsplash.com/photo-1581147036324-c47a03a81d48?w=800", new[]{("Piezas","12"),("Material","Acero cromo-vanadio")}, new[]{"Puntas magnéticas.","Estuche incluido."}),
            ("Automotriz","Accesorios","Cargador USB para auto doble puerto","AutoCharge","Carga rápida y segura en el vehículo.",12.99m,9.99m,150,"https://images.unsplash.com/photo-1617788138017-80ad40651399?w=800", new[]{("Puertos","2"),("Salida","QC 3.0")}, new[]{"Protección contra sobrecarga.","Diseño compacto."}),
            ("Bebé","Alimentación","Set de biberones anticólicos 3 piezas","BabySoft","Flujo natural y materiales seguros.",22.99m,18.99m,55,"https://images.unsplash.com/photo-1515488042361-ee00e0ddd4e4?w=800", new[]{("Piezas","3"),("Material","Libre de BPA")}, new[]{"Válvula anticólicos.","Fácil de limpiar."}),
            ("Moda para Hombre","Ropa","Camiseta básica de algodón","UrbanWear","Comodidad diaria y corte clásico.",14.99m,11.99m,100,"https://images.unsplash.com/photo-1521572163474-6864f9cf17ab?w=800", new[]{("Material","100% algodón"),("Tallas","S-XL")}, new[]{"Suave al tacto.","Transpirable."}),
            ("Moda para Mujer","Ropa","Blusa casual manga corta","StyleHer","Ligera y versátil.",19.99m,15.99m,85,"https://images.unsplash.com/photo-1483985988355-763728e1935b?w=800", new[]{("Material","Poliéster/algodón"),("Tallas","S-L")}, new[]{"Corte moderno.","Fácil de combinar."}),
            ("Videojuegos","Accesorios","Control inalámbrico multiplataforma","GamePro","Respuesta precisa y diseño ergonómico.",49.99m,42.99m,45,"https://images.unsplash.com/photo-1605901309584-818e25960a8b?w=800", new[]{("Conectividad","Bluetooth/USB"),("Compatibilidad","PC/Consola")}, new[]{"Batería recargable.","Vibración háptica."}),
            ("Salud y Hogar","Bienestar","Termómetro digital infrarrojo","HealthPlus","Medición rápida y precisa.",27.99m,22.99m,65,"https://images.unsplash.com/photo-1582719478250-c89cae4dc85b?w=800", new[]{("Tipo","Infrarrojo"),("Tiempo","1 segundo")}, new[]{"Pantalla retroiluminada.","Memoria de lecturas."}),
            ("Equipaje","Mochilas","Mochila urbana resistente al agua","TravelLite","Ideal para estudio y trabajo.",39.50m,31.99m,60,"https://images.unsplash.com/photo-1553062407-98eeb64c6a62?w=800", new[]{("Capacidad","25 L"),("Material","Poliéster")}, new[]{"Compartimento para laptop.","Correas acolchadas."}),
            ("Arte y artesanías","Pintura","Set de acrílicos 24 colores","ArtMix","Colores vibrantes para proyectos creativos.",16.99m,13.49m,90,"https://images.unsplash.com/photo-1513364776144-60967b0f800f?w=800", new[]{("Colores","24"),("Contenido","12 ml c/u")}, new[]{"Secado rápido.","Alta pigmentación."}),
            ("Cine y TV","Streaming","Control remoto universal","UniRemote","Compatible con múltiples dispositivos.",18.50m,14.99m,70,"https://images.unsplash.com/photo-1593359677879-a4bb92f829d1?w=800", new[]{("Compatibilidad","TV/Streaming"),("Alimentación","AAA")}, new[]{"Configuración sencilla.","Botones iluminados."}),
            ("Industria y Ciencia","Medición","Calibrador digital de precisión","MechTech","Mediciones confiables para taller.",29.99m,24.99m,40,"https://images.unsplash.com/photo-1581092160562-40aa08e78837?w=800", new[]{("Rango","150 mm"),("Precisión","0.01 mm")}, new[]{"Pantalla LCD.","Cuerpo de acero."}),
            ("Música, CDs y Vinilos","Vinilos","Vinilo clásico edición especial","RetroSound","Sonido cálido para coleccionistas.",34.99m,null,25,"https://images.unsplash.com/photo-1603048588665-791ca8aea617?w=800", new[]{("Formato","Vinilo 12\""),("Edición","Especial")}, new[]{"Incluye funda ilustrada.","Remasterizado."}),
            ("Software","Productividad","Licencia de suite ofimática anual","OfficeSuite","Herramientas esenciales para productividad.",59.99m,49.99m,200,"https://images.unsplash.com/photo-1517694712202-14dd9538aa97?w=800", new[]{("Licencia","1 año"),("Usuarios","1")}, new[]{"Procesador de texto y hojas de cálculo.","Actualizaciones incluidas."}),
            ("Tienda Kindle","E-readers","Funda protectora para lector electrónico","KindleCase","Protección ligera y resistente.",17.99m,14.99m,80,"https://images.unsplash.com/photo-1512820790803-83ca734da794?w=800", new[]{("Compatibilidad","6-7 pulgadas"),("Material","PU")}, new[]{"Cierre magnético.","Modo soporte."}),
            ("Moda para Niñas","Calzado","Zapatillas deportivas infantiles","KidsRun","Comodidad para el día a día.",24.99m,19.99m,60,"https://images.unsplash.com/photo-1514989940723-e8e51635b782?w=800", new[]{("Tallas","28-34"),("Material","Sintético/textil")}, new[]{"Suela antideslizante.","Livianas."}),
            ("Moda para Niños","Ropa","Conjunto deportivo infantil","PlayKids","Ideal para juego y actividad.",26.99m,21.99m,55,"https://images.unsplash.com/photo-1519238263530-99bdd11df2ea?w=800", new[]{("Material","Algodón/poliéster"),("Tallas","4-10")}, new[]{"Tela cómoda.","Fácil lavado."}),
            ("Prime Video","Accesorios","Soporte ajustable para TV","ViewFlex","Mejor ángulo para tu sala.",39.99m,33.99m,30,"https://images.unsplash.com/photo-1461151304267-38535e780c79?w=800", new[]{("Compatibilidad","32-55\""),("Material","Acero")}, new[]{"Inclinación ajustable.","Instalación sencilla."}),
            ("Música MP3","Audio","Reproductor MP3 portátil 32GB","TuneGo","Música en cualquier lugar.",29.90m,24.90m,45,"https://images.unsplash.com/photo-1511379938547-c1f69419868d?w=800", new[]{("Almacenamiento","32 GB"),("Batería","20 horas")}, new[]{"Ligero y compacto.","Radio FM integrada."}),
            ("Ofertas","Promociones","Pack de organización para cocina 6 piezas","HomePack","Ahorra espacio y mantén el orden.",27.99m,18.99m,100,"https://images.unsplash.com/photo-1584622650111-993a426fbf0a?w=800", new[]{("Piezas","6"),("Material","Plástico libre de BPA")}, new[]{"Apilables.","Tapas herméticas."}),
            ("Hogar y cocina","Organización","Organizador de despensa transparente","ClearStore","Visualiza y ordena tus alimentos.",21.99m,16.99m,70,"https://images.unsplash.com/photo-1610701596007-11502861dcfa?w=800", new[]{("Material","Plástico"),("Uso","Despensa")}, new[]{"Diseño apilable.","Fácil limpieza."}),
            ("Electrónicos","Cargadores","Cable USB-C reforzado 2m","ChargeFlex","Carga y datos de alta velocidad.",11.99m,8.99m,200,"https://images.unsplash.com/photo-1583863788434-e58a36330cf0?w=800", new[]{("Longitud","2 m"),("Conector","USB-C")}, new[]{"Recubrimiento trenzado.","Compatibilidad amplia."}),
            ("Computadoras","Almacenamiento","Disco SSD portátil 1TB","DataSwift","Rendimiento veloz y compacto.",109.99m,89.99m,35,"https://images.unsplash.com/photo-1597852074816-d933c7d2b988?w=800", new[]{("Capacidad","1 TB"),("Interfaz","USB 3.2")}, new[]{"Transferencias rápidas.","Resistente a golpes."}),
            ("Deportes y actividades al aire libre","Fitness","Bandas de resistencia set 5 niveles","FitBand","Entrena en casa o al aire libre.",19.99m,15.49m,110,"https://images.unsplash.com/photo-1598289431512-b97b0917affc?w=800", new[]{("Niveles","5"),("Material","Látex")}, new[]{"Incluye bolsa.","Diferentes intensidades."}),
            ("Belleza y cuidado personal","Cabello","Shampoo nutritivo 400ml","HairGlow","Brillo y suavidad diaria.",13.49m,10.99m,95,"https://images.unsplash.com/photo-1526947425960-945c6e72858f?w=800", new[]{("Contenido","400 ml"),("Tipo","Nutritivo")}, new[]{"Aroma suave.","Para uso diario."}),
            ("Libros","Desarrollo personal","El poder del ahora","Editorial Gaia","Reflexiones para una vida consciente.",18.50m,14.99m,50,"https://images.unsplash.com/photo-1512820790803-83ca734da794?w=800", new[]{("Formato","Tapa blanda"),("Idioma","Español")}, new[]{"Lectura transformadora.","Edición popular."}),
            ("Herramientas y mejoramiento del hogar","Iluminación","Foco LED inteligente WiFi","BrightHome","Control desde tu celular.",16.99m,12.99m,85,"https://images.unsplash.com/photo-1550989460-0adf9ea622e2?w=800", new[]{("Potencia","9W"),("Conectividad","WiFi")}, new[]{"Compatible con asistentes.","Ahorro de energía."}),
            ("Automotriz","Limpieza","Kit de limpieza interior para auto","AutoCare","Mantén tu vehículo impecable.",23.99m,18.99m,60,"https://images.unsplash.com/photo-1607860108855-64acf2078ed9?w=800", new[]{("Incluye","Paños y cepillos"),("Uso","Interior")}, new[]{"Materiales suaves.","Resultados rápidos."}),
            ("Salud y Hogar","Cuidado personal","Báscula digital de baño","FitScale","Control preciso de tu peso.",28.99m,22.49m,50,"https://images.unsplash.com/photo-1576678927484-cc907957088c?w=800", new[]{("Capacidad","180 kg"),("Pantalla","LCD")}, new[]{"Plataforma de vidrio templado.","Encendido automático."}),
            ("Juguetes y juegos","Juegos de mesa","Juego de estrategia familiar","FamilyFun","Diversión para todas las edades.",32.99m,26.99m,40,"https://images.unsplash.com/photo-1610890716171-6b1bb98ffd09?w=800", new[]{("Jugadores","2-6"),("Edad","8+")}, new[]{"Partidas dinámicas.","Componentes duraderos."}),
            ("Insumos para mascotas","Gatos","Rascador compacto para gatos","CatJoy","Entretenimiento y cuidado de uñas.",29.99m,23.99m,45,"https://images.unsplash.com/photo-1545249390-6bdfa286032f?w=800", new[]{("Material","Sisal/cartón"),("Tamaño","Compacto")}, new[]{"Fácil de ubicar.","Resistente."}),
            ("Equipaje","Accesorios de viaje","Organizadores de maleta 4 piezas","PackTrip","Optimiza el espacio al viajar.",21.50m,16.50m,75,"https://images.unsplash.com/photo-1488646953014-85cb44e25828?w=800", new[]{("Piezas","4"),("Material","Tela ligera")}, new[]{"Cierres resistentes.","Varios tamaños."}),
            ("Hogar y cocina","Utensilios","Juego de cuchillos de cocina 5 piezas","SharpChef","Cortes precisos para preparaciones diarias.",33.99m,27.49m,55,"https://images.unsplash.com/photo-1593618998160-e34014e67546?w=800", new[]{("Piezas","5"),("Material","Acero inoxidable")}, new[]{"Mangos ergonómicos.","Base organizadora incluida."}),
            ("Electrónicos","Smart home","Enchufe inteligente WiFi","SmartPlug","Controla tus dispositivos desde el móvil.",14.99m,11.49m,120,"https://images.unsplash.com/photo-1558002038-1055907df827?w=800", new[]{("Conectividad","WiFi"),("Voltaje","110-240V")}, new[]{"Programación de horarios.","Compatible con asistentes."})
        };
        foreach (var x in productos)
        {
            var p = Producto.Crear(Cat(x.cat).Id, null, x.sub, x.nombre, x.marca, x.desc, x.precio, x.oferta, x.stock, x.img);
            db.Productos.Add(p);
            foreach (var m in new[]{("imagen", x.img, 1)}) db.ProductoMedias.Add(ProductoMedia.Crear(p.Id, m.Item1, m.Item2, m.Item3));
            var ord = 0; foreach (var f in x.ficha) db.ProductoFichasTecnicas.Add(ProductoFichaTecnica.Crear(p.Id, f.k, f.v, ord++));
            ord = 0; foreach (var b in x.bullets) db.ProductoCaracteristicas.Add(ProductoCaracteristica.Crear(p.Id, b, ord++));
        }
        await db.SaveChangesAsync(ct);
    }
}
