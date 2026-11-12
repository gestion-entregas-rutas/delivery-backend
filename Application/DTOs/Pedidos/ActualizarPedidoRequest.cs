namespace Application.DTOs;

/// <summary>Reemplazo completo de un pedido pendiente. Version es la última versión consultada.</summary>
public sealed record ActualizarPedidoRequest(string ClienteNombre, string ClienteTelefono, string Direccion,
    double Latitud, double Longitud, DateOnly FechaEntrega, TimeOnly HoraEntrega, uint Version,
    Guid? ZonaId = null, DetallesPedidoRequest? Detalles = null);
