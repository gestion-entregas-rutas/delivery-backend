using Domain.Common;
using Domain.Enums;
using Domain.Exceptions;
using Domain.ValueObjects;

namespace Domain.Entities;

public class Pedido : Entity
{
    private Pedido() { } 

    public string ClienteNombre { get; private set; } = null!;
    public string ClienteTelefono { get; private set; } = null!;
    public string Direccion { get; private set; } = null!;
    public GeoPoint Ubicacion { get; private set; }
    public Guid ZonaId { get; private set; }

    // Detaller del pedido
    public DateOnly FechaEntrega { get; private set; }
    public TimeOnly HoraEntrega { get; private set; }
    public DateTimeOffset RegistradoEn { get; private set; }
    public DateTimeOffset? EntregadoEn { get; private set; }

    public EstadoPedido Estado { get; private set; }
    public Guid? RutaId { get; private set; }

    public bool EsDeUltimoMomento(DateOnly hoy) => FechaEntrega == hoy;

    public static Pedido Registrar(
        string clienteNombre,
        string clienteTelefono,
        string direccion,
        GeoPoint ubicacion,
        Zona zona,
        DateOnly fechaEntrega,
        TimeOnly horaEntrega,
        DateOnly hoy,
        DateTimeOffset ahora)
    {
        if (string.IsNullOrWhiteSpace(clienteNombre))
            throw new DomainException("El pedido debe indicar el nombre del cliente.");
        if (string.IsNullOrWhiteSpace(clienteTelefono))
            throw new DomainException("El pedido debe indicar el teléfono del cliente.");
        if (string.IsNullOrWhiteSpace(direccion))
            throw new DomainException("El pedido debe indicar la dirección de entrega.");
        if (fechaEntrega < hoy)
            throw new DomainException("La fecha de entrega no puede estar en el pasado.");
        if (!zona.Activa)
            throw new DomainException($"La zona '{zona.Nombre}' no está activa.");
        if (!zona.Contiene(ubicacion))
            throw new DomainException($"La dirección no pertenece a la zona '{zona.Nombre}'.");

        return new Pedido
        {
            ClienteNombre = clienteNombre.Trim(),
            ClienteTelefono = clienteTelefono.Trim(),
            Direccion = direccion.Trim(),
            Ubicacion = ubicacion,
            ZonaId = zona.Id,
            FechaEntrega = fechaEntrega,
            HoraEntrega = horaEntrega,
            RegistradoEn = ahora,
            Estado = EstadoPedido.Pendiente
        };
    }

    public void AsignarARuta(Guid rutaId)
    {
        if (Estado != EstadoPedido.Pendiente)
            throw new TransicionInvalidaException(nameof(Pedido), Estado, EstadoPedido.AsignadoARuta);

        RutaId = rutaId;
        Estado = EstadoPedido.AsignadoARuta;
    }

    public void Desasignar()
    {
        if (Estado != EstadoPedido.AsignadoARuta)
            throw new TransicionInvalidaException(nameof(Pedido), Estado, EstadoPedido.Pendiente);

        RutaId = null;
        Estado = EstadoPedido.Pendiente;
    }

    public void MarcarEnCamino()
    {
        if (Estado != EstadoPedido.AsignadoARuta)
            throw new TransicionInvalidaException(nameof(Pedido), Estado, EstadoPedido.EnCamino);

        Estado = EstadoPedido.EnCamino;
    }

    public void MarcarEntregado(DateTimeOffset ahora)
    {
        if (Estado != EstadoPedido.EnCamino)
            throw new TransicionInvalidaException(nameof(Pedido), Estado, EstadoPedido.Entregado);

        Estado = EstadoPedido.Entregado;
        EntregadoEn = ahora;
    }

    public void Cancelar()
    {
        if (Estado != EstadoPedido.Pendiente)
            throw new TransicionInvalidaException(nameof(Pedido), Estado, EstadoPedido.Cancelado);

        Estado = EstadoPedido.Cancelado;
    }
}
