using Application.DTOs;
using Application.Exceptions;
using Application.Interfaces;
using Domain.Entities;
using Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Application.Services;

public sealed class CarritoService(IAppDbContext db, TimeProvider reloj) : ICarritoService
{
    public async Task<CarritoDto> ObtenerAsync(Guid? usuarioId, string? tokenInvitado, bool crearSiNoExiste, CancellationToken ct)
    {
        var carrito = await ResolverCarritoAsync(usuarioId, tokenInvitado, crearSiNoExiste, ct);
        if (carrito is null) return CarritoVacio(tokenInvitado);
        return await ConstruirDtoAsync(carrito, ct);
    }

    public async Task<CarritoDto> AgregarItemAsync(Guid? usuarioId, string? tokenInvitado, Guid productoId, int cantidad, CancellationToken ct)
    {
        if (cantidad <= 0) throw new DomainException("La cantidad debe ser mayor a 0.");
        var carrito = await ResolverCarritoAsync(usuarioId, tokenInvitado, true, ct) ?? throw new DomainException("No se pudo obtener el carrito.");
        var producto = await db.Productos.AsNoTracking().FirstOrDefaultAsync(p => p.Id == productoId, ct) ?? throw new NoEncontradoException(nameof(Producto), productoId);
        if (!producto.Activo) throw new DomainException("El producto no está disponible.");
        var item = await db.CarritoItems.FirstOrDefaultAsync(i => i.CarritoId == carrito.Id && i.ProductoId == productoId, ct);
        var nuevaCantidad = (item?.Cantidad ?? 0) + cantidad;
        if (nuevaCantidad > producto.Stock) nuevaCantidad = producto.Stock;
        if (nuevaCantidad <= 0) throw new DomainException("El producto no tiene stock disponible.");
        if (item is null)
            db.CarritoItems.Add(CarritoItem.Crear(carrito.Id, productoId, nuevaCantidad));
        else
            item.CambiarCantidad(nuevaCantidad);
        carrito.Tocar(reloj.GetUtcNow());
        await db.SaveChangesAsync(ct);
        return await ConstruirDtoAsync(carrito, ct);
    }

    public async Task<CarritoDto> ActualizarItemAsync(Guid? usuarioId, string? tokenInvitado, Guid productoId, int cantidad, CancellationToken ct)
    {
        var carrito = await ResolverCarritoAsync(usuarioId, tokenInvitado, false, ct) ?? throw new NoEncontradoException("Carrito", Guid.Empty);
        var item = await db.CarritoItems.FirstOrDefaultAsync(i => i.CarritoId == carrito.Id && i.ProductoId == productoId, ct)
            ?? throw new NoEncontradoException("Item de carrito", productoId);
        if (cantidad <= 0)
        {
            db.CarritoItems.Remove(item);
        }
        else
        {
            var producto = await db.Productos.AsNoTracking().FirstOrDefaultAsync(p => p.Id == productoId, ct);
            var max = producto?.Stock ?? cantidad;
            item.CambiarCantidad(Math.Min(cantidad, Math.Max(0, max)));
            if (item.Cantidad <= 0) db.CarritoItems.Remove(item);
        }
        carrito.Tocar(reloj.GetUtcNow());
        await db.SaveChangesAsync(ct);
        return await ConstruirDtoAsync(carrito, ct);
    }

    public async Task<CarritoDto> QuitarItemAsync(Guid? usuarioId, string? tokenInvitado, Guid productoId, CancellationToken ct)
    {
        var carrito = await ResolverCarritoAsync(usuarioId, tokenInvitado, false, ct) ?? throw new NoEncontradoException("Carrito", Guid.Empty);
        var item = await db.CarritoItems.FirstOrDefaultAsync(i => i.CarritoId == carrito.Id && i.ProductoId == productoId, ct);
        if (item is not null) db.CarritoItems.Remove(item);
        carrito.Tocar(reloj.GetUtcNow());
        await db.SaveChangesAsync(ct);
        return await ConstruirDtoAsync(carrito, ct);
    }

    public async Task<CarritoDto> MergeAsync(Guid usuarioId, string tokenInvitado, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(tokenInvitado)) throw new DomainException("Se requiere el token del carrito invitado.");
        var invitado = await db.Carritos.Include(c => c.Items).FirstOrDefaultAsync(c => c.TokenInvitado == tokenInvitado, ct);
        var usuario = await db.Carritos.Include(c => c.Items).FirstOrDefaultAsync(c => c.UsuarioId == usuarioId, ct);
        if (invitado is null)
            return await ObtenerAsync(usuarioId, null, true, ct);
        if (usuario is null)
        {
            invitado.AsignarUsuario(usuarioId);
            usuario = invitado;
        }
        else if (usuario.Id != invitado.Id)
        {
            var itemsInv = await db.CarritoItems.Where(i => i.CarritoId == invitado.Id).ToListAsync(ct);
            var itemsUsr = await db.CarritoItems.Where(i => i.CarritoId == usuario.Id).ToListAsync(ct);
            foreach (var ii in itemsInv)
            {
                var prod = await db.Productos.AsNoTracking().FirstOrDefaultAsync(p => p.Id == ii.ProductoId, ct);
                if (prod is null || !prod.Activo) continue;
                var existente = itemsUsr.FirstOrDefault(x => x.ProductoId == ii.ProductoId);
                var combinada = (existente?.Cantidad ?? 0) + ii.Cantidad;
                combinada = Math.Min(combinada, prod.Stock);
                if (combinada <= 0) continue;
                if (existente is null)
                {
                    var nuevo = CarritoItem.Crear(usuario.Id, ii.ProductoId, combinada);
                    db.CarritoItems.Add(nuevo);
                    itemsUsr.Add(nuevo);
                }
                else existente.CambiarCantidad(combinada);
            }
            db.CarritoItems.RemoveRange(itemsInv);
            db.Carritos.Remove(invitado);
        }
        usuario.Tocar(reloj.GetUtcNow());
        await db.SaveChangesAsync(ct);
        return await ConstruirDtoAsync(usuario, ct);
    }

    public async Task<CotizacionDto> CotizarAsync(Guid? usuarioId, string? tokenInvitado, CancellationToken ct)
    {
        var carrito = await ResolverCarritoAsync(usuarioId, tokenInvitado, false, ct);
        if (carrito is null) return new CotizacionDto([], 0, 0, 0, 0, false, []);
        var items = await db.CarritoItems.AsNoTracking().Where(i => i.CarritoId == carrito.Id).ToListAsync(ct);
        var ids = items.Select(i => i.ProductoId).Distinct().ToList();
        var productos = await db.Productos.AsNoTracking().Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
        var catsActivas = await db.Categorias.AsNoTracking().Where(c => c.Activa).Select(c => c.Id).ToListAsync(ct);
        var subsInactivas = await db.Subcategorias.AsNoTracking().Where(s => !s.Activa).Select(s => s.Id).ToListAsync(ct);

        var cotItems = new List<CotizacionItemDto>();
        var advertencias = new List<string>();
        var requiereRevision = false;
        decimal subtotal = 0;
        foreach (var it in items)
        {
            if (!productos.TryGetValue(it.ProductoId, out var p))
            {
                requiereRevision = true;
                advertencias.Add("Un producto del carrito ya no existe y fue marcado para revisión.");
                cotItems.Add(new CotizacionItemDto(it.ProductoId, "Producto no disponible", it.Cantidad, 0, 0, 0, false, "Producto no encontrado."));
                continue;
            }
            var disponible = p.Activo && catsActivas.Contains(p.CategoriaId) && (p.SubcategoriaId == null || !subsInactivas.Contains(p.SubcategoriaId.Value)) && p.Stock > 0;
            var cantFinal = disponible ? Math.Min(it.Cantidad, p.Stock) : 0;
            if (!disponible)
            {
                requiereRevision = true;
                advertencias.Add($"'{p.Nombre}' no está disponible.");
            }
            else if (cantFinal != it.Cantidad)
            {
                requiereRevision = true;
                advertencias.Add($"'{p.Nombre}': cantidad ajustada a {cantFinal} por stock disponible.");
            }
            var precio = p.PrecioOferta ?? p.PrecioBase;
            var sub = precio * cantFinal;
            subtotal += sub;
            cotItems.Add(new CotizacionItemDto(p.Id, p.Nombre, it.Cantidad, cantFinal, precio, sub, disponible && cantFinal > 0, disponible ? (cantFinal != it.Cantidad ? "Cantidad ajustada por stock." : null) : "Producto no disponible."));
        }
        var descuento = 0m; // descuentos adicionales no definidos
        var envio = 0m; // se calcula según entrega en checkout
        var total = subtotal - descuento + envio;
        return new CotizacionDto(cotItems, subtotal, descuento, envio, total, requiereRevision, advertencias.Distinct().ToList());
    }

    private async Task<Carrito?> ResolverCarritoAsync(Guid? usuarioId, string? tokenInvitado, bool crearSiNoExiste, CancellationToken ct)
    {
        if (usuarioId.HasValue)
        {
            var c = await db.Carritos.FirstOrDefaultAsync(x => x.UsuarioId == usuarioId.Value, ct);
            if (c is not null || !crearSiNoExiste) return c;
            c = Carrito.CrearUsuario(usuarioId.Value, reloj.GetUtcNow());
            db.Carritos.Add(c);
            await db.SaveChangesAsync(ct);
            return c;
        }
        if (!string.IsNullOrWhiteSpace(tokenInvitado))
        {
            var c = await db.Carritos.FirstOrDefaultAsync(x => x.TokenInvitado == tokenInvitado, ct);
            if (c is not null || !crearSiNoExiste) return c;
        }
        if (!crearSiNoExiste) return null;
        var token = string.IsNullOrWhiteSpace(tokenInvitado) ? Guid.NewGuid().ToString("N") : tokenInvitado;
        var nuevo = Carrito.CrearInvitado(token, reloj.GetUtcNow());
        db.Carritos.Add(nuevo);
        await db.SaveChangesAsync(ct);
        return nuevo;
    }

    private async Task<CarritoDto> ConstruirDtoAsync(Carrito carrito, CancellationToken ct)
    {
        var items = await db.CarritoItems.AsNoTracking().Where(i => i.CarritoId == carrito.Id).ToListAsync(ct);
        if (items.Count == 0) return new CarritoDto(carrito.Id, carrito.TokenInvitado, [], 0, 0, 0, 0, []);
        var ids = items.Select(i => i.ProductoId).Distinct().ToList();
        var productos = await db.Productos.AsNoTracking().Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
        var catsActivas = await db.Categorias.AsNoTracking().Where(c => c.Activa).Select(c => c.Id).ToListAsync(ct);
        var subsInactivas = await db.Subcategorias.AsNoTracking().Where(s => !s.Activa).Select(s => s.Id).ToListAsync(ct);
        var dtos = new List<CarritoItemDto>();
        var advertencias = new List<string>();
        decimal subtotal = 0;
        foreach (var it in items)
        {
            if (!productos.TryGetValue(it.ProductoId, out var p))
            {
                advertencias.Add("Un producto del carrito ya no está disponible.");
                continue;
            }
            var activoOk = p.Activo && catsActivas.Contains(p.CategoriaId) && (p.SubcategoriaId == null || !subsInactivas.Contains(p.SubcategoriaId.Value));
            var disponible = activoOk && p.Stock >= it.Cantidad && p.Stock > 0;
            var precio = p.PrecioOferta ?? p.PrecioBase;
            var sub = precio * it.Cantidad;
            if (disponible) subtotal += sub;
            string? adv = null;
            if (!activoOk) { adv = "Producto no disponible."; advertencias.Add($"'{p.Nombre}' no está disponible."); }
            else if (p.Stock < it.Cantidad) { adv = $"Stock insuficiente (disponible: {p.Stock})."; advertencias.Add($"'{p.Nombre}' tiene stock insuficiente."); }
            dtos.Add(new CarritoItemDto(p.Id, p.Nombre, p.ImagenPrincipalUrl, p.PrecioBase, p.PrecioOferta, precio, it.Cantidad, p.Stock, activoOk, disponible, sub, adv));
        }
        return new CarritoDto(carrito.Id, carrito.TokenInvitado, dtos, subtotal, 0, 0, subtotal, advertencias.Distinct().ToList());
    }

    private static CarritoDto CarritoVacio(string? token) => new(Guid.Empty, token, [], 0, 0, 0, 0, []);
}
