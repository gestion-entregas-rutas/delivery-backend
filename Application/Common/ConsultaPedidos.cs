using Application.DTOs;
using Domain.Entities;
using Domain.Enums;
using Domain.Exceptions;
namespace Application.Common;

internal static class ConsultaPedidos
{
    public static IQueryable<Pedido> Filtrar(this IQueryable<Pedido> q, FiltroPedidos f)
    {
        if (f.Desde.HasValue && f.Hasta.HasValue && f.Desde > f.Hasta)
            throw new DomainException("Desde no puede ser posterior a Hasta.");
        if (f.Fecha.HasValue && (f.Desde.HasValue || f.Hasta.HasValue))
            throw new DomainException("Use Fecha o un rango Desde/Hasta, no ambos.");
        if (f.Estado.HasValue && !Enum.IsDefined(f.Estado.Value)) throw new DomainException("Estado de pedido inválido.");
        if (f.Buscar?.Length > 150) throw new DomainException("La búsqueda admite hasta 150 caracteres.");
        if (f.Fecha is { } fecha) q = q.Where(p => p.FechaEntrega == fecha);
        if (f.Desde is { } desde) q = q.Where(p => p.FechaEntrega >= desde);
        if (f.Hasta is { } hasta) q = q.Where(p => p.FechaEntrega <= hasta);
        if (f.ZonaId is { } zona) q = q.Where(p => p.ZonaId == zona);
        if (f.Estado is { } estado) q = q.Where(p => p.Estado == estado);
        if (!string.IsNullOrWhiteSpace(f.Buscar))
        {
            var texto = f.Buscar.Trim().ToLowerInvariant();
            q = q.Where(p => p.ClienteNombre.ToLower().Contains(texto) || p.ClienteTelefono.Contains(texto) ||
                p.Direccion.ToLower().Contains(texto) || (p.Detalles != null && p.Detalles.RemitenteNombre != null &&
                p.Detalles.RemitenteNombre.ToLower().Contains(texto)));
        }
        return q;
    }
}
