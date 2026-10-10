using Domain.Common;
using Domain.Enums;
using Domain.Exceptions;

namespace Domain.Entities;

public class Ruta : Entity
{
    private readonly List<ParadaRuta> _paradas = [];

    private Ruta() { } // EF Core

    public DateOnly Fecha { get; private set; }
    public Guid ZonaId { get; private set; }
    public Guid RepartidorId { get; private set; }

    public int Capacidad { get; private set; }

    public EstadoRuta Estado { get; private set; }
    public DateTimeOffset CreadaEn { get; private set; }
    public DateTimeOffset? IniciadaEn { get; private set; }
    public DateTimeOffset? FinalizadaEn { get; private set; }

    // Al cargar desde la BD la lista no llega ordenada: siempre se expone según Orden.
    public IReadOnlyList<ParadaRuta> Paradas => _paradas.OrderBy(p => p.Orden).ToList();

    public bool TieneEspacio => _paradas.Count < Capacidad;

    private bool AceptaCambios => Estado is EstadoRuta.Planificada or EstadoRuta.EnCurso;

    public int PosicionMinimaInsercion =>
        Estado == EstadoRuta.EnCurso
            ? _paradas.Count(p => p.Pedido.Estado is EstadoPedido.EnCamino or EstadoPedido.Entregado)
            : 0;

    public static Ruta Crear(Zona zona, Repartidor repartidor, DateOnly fecha, DateTimeOffset ahora)
    {
        if (!zona.Activa)
            throw new DomainException($"La zona '{zona.Nombre}' no está activa.");
        if (!repartidor.EstaDisponible)
            throw new DomainException($"El repartidor '{repartidor.Nombre}' no está disponible.");

        return new Ruta
        {
            Fecha = fecha,
            ZonaId = zona.Id,
            RepartidorId = repartidor.Id,
            Capacidad = Math.Min(zona.MaxPedidosPorRuta, repartidor.CapacidadMaxima),
            Estado = EstadoRuta.Planificada,
            CreadaEn = ahora
        };
    }

    public void AgregarPedido(Pedido pedido, int? posicion = null)
    {
        if (!AceptaCambios)
            throw new DomainException($"No se pueden agregar pedidos a una ruta '{Estado}'.");
        if (pedido.ZonaId != ZonaId)
            throw new DomainException("El pedido pertenece a otra zona.");
        if (pedido.FechaEntrega != Fecha)
            throw new DomainException("El pedido es de otra fecha de entrega.");
        if (!TieneEspacio)
            throw new DomainException("La ruta alcanzó su capacidad máxima.");

        Ordenar();
        var indice = posicion ?? _paradas.Count;
        if (indice < PosicionMinimaInsercion || indice > _paradas.Count)
            throw new DomainException($"Posición de inserción inválida: {indice}.");

        pedido.AsignarARuta(Id); 

        _paradas.Insert(indice, new ParadaRuta(Id, pedido, indice + 1));
        Reordenar();
    }

    public void QuitarPedido(Guid pedidoId)
    {
        if (!AceptaCambios)
            throw new DomainException($"No se pueden quitar pedidos de una ruta '{Estado}'.");

        Ordenar();
        var parada = BuscarParada(pedidoId);
        parada.Pedido.Desasignar();
        _paradas.Remove(parada);
        Reordenar();
    }

    public void Iniciar(Repartidor repartidor, DateTimeOffset ahora)
    {
        if (Estado != EstadoRuta.Planificada)
            throw new TransicionInvalidaException(nameof(Ruta), Estado, EstadoRuta.EnCurso);
        if (repartidor.Id != RepartidorId)
            throw new DomainException("La ruta pertenece a otro repartidor.");
        if (_paradas.Count == 0)
            throw new DomainException("No se puede iniciar una ruta sin pedidos.");

        repartidor.IniciarSalida();
        Estado = EstadoRuta.EnCurso;
        IniciadaEn = ahora;
    }

    public void MarcarEnCamino(Guid pedidoId)
    {
        ExigirEnCurso();
        BuscarParada(pedidoId).Pedido.MarcarEnCamino();
    }

    public void RegistrarEntrega(Guid pedidoId, Repartidor repartidor, DateTimeOffset ahora)
    {
        ExigirEnCurso();
        if (repartidor.Id != RepartidorId)
            throw new DomainException("La ruta pertenece a otro repartidor.");

        BuscarParada(pedidoId).Pedido.MarcarEntregado(ahora);

        if (_paradas.All(p => p.Pedido.Estado == EstadoPedido.Entregado))
        {
            Estado = EstadoRuta.Completada;
            FinalizadaEn = ahora;
            repartidor.FinalizarSalida();
        }
    }

    public void Cancelar()
    {
        if (Estado != EstadoRuta.Planificada)
            throw new TransicionInvalidaException(nameof(Ruta), Estado, EstadoRuta.Cancelada);

        foreach (var parada in _paradas)
            parada.Pedido.Desasignar();

        _paradas.Clear();
        Estado = EstadoRuta.Cancelada;
    }

    private void ExigirEnCurso()
    {
        if (Estado != EstadoRuta.EnCurso)
            throw new DomainException($"La ruta debe estar en curso (estado actual: '{Estado}').");
    }

    private ParadaRuta BuscarParada(Guid pedidoId) =>
        _paradas.FirstOrDefault(p => p.PedidoId == pedidoId)
        ?? throw new DomainException("El pedido no pertenece a esta ruta.");

    private void Ordenar() => _paradas.Sort(static (a, b) => a.Orden.CompareTo(b.Orden));

    private void Reordenar()
    {
        for (var i = 0; i < _paradas.Count; i++)
            _paradas[i].Orden = i + 1;
    }
}
