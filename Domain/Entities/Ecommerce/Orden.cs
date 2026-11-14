using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

public sealed class Orden : Entity
{
    private readonly List<DetalleOrden> _detalles = [];
    private Orden() { }
    public string NumeroOrden { get; private set; } = null!;
    public Guid ClienteId { get; private set; }
    public EstadoOrden Estado { get; private set; }
    public EstadoPago EstadoPago { get; private set; }
    public TipoEntrega TipoEntrega { get; private set; }
    public decimal Total { get; private set; }
    public DateTimeOffset CreadaEn { get; private set; }
    public string? CodigoRetiro { get; private set; }
    public Guid? PedidoLogisticoId { get; private set; }
    public string? IdempotencyKey { get; private set; }

    // Retiro
    public string? RetiroCedula { get; private set; }
    public string? RetiroNombre { get; private set; }
    public string? RetiroTelefono { get; private set; }
    public DateOnly? RetiroFecha { get; private set; }
    public string? RetiroFranja { get; private set; }

    // Domicilio
    public string? DestinatarioNombre { get; private set; }
    public string? DestinatarioTelefono { get; private set; }
    public string? DireccionTexto { get; private set; }
    public double? Latitud { get; private set; }
    public double? Longitud { get; private set; }
    public DateOnly? FechaEntrega { get; private set; }
    public TimeOnly? HoraEntrega { get; private set; }
    public string? ObservacionesEntrega { get; private set; }

    public IReadOnlyCollection<DetalleOrden> Detalles => _detalles;

    public static Orden Crear(string numeroOrden, Guid clienteId, TipoEntrega tipoEntrega, decimal total, DateTimeOffset ahora, string? idempotencyKey)
    {
        return new Orden
        {
            NumeroOrden = numeroOrden,
            ClienteId = clienteId,
            TipoEntrega = tipoEntrega,
            Total = total,
            Estado = EstadoOrden.PendientePago,
            EstadoPago = Enums.EstadoPago.Pendiente,
            CreadaEn = ahora,
            IdempotencyKey = idempotencyKey
        };
    }

    public void AgregarDetalle(DetalleOrden detalle) => _detalles.Add(detalle);

    public void ConfigurarRetiro(string cedula, string nombre, string telefono, DateOnly fecha, string franja, string codigoRetiro)
    {
        RetiroCedula = cedula; RetiroNombre = nombre; RetiroTelefono = telefono; RetiroFecha = fecha; RetiroFranja = franja; CodigoRetiro = codigoRetiro;
    }

    public void ConfigurarDomicilio(string nombre, string telefono, string direccion, double lat, double lon, DateOnly fecha, TimeOnly hora, string? observaciones)
    {
        DestinatarioNombre = nombre; DestinatarioTelefono = telefono; DireccionTexto = direccion;
        Latitud = lat; Longitud = lon; FechaEntrega = fecha; HoraEntrega = hora; ObservacionesEntrega = observaciones;
    }

    public void MarcarPagada(Guid? pedidoLogisticoId)
    {
        Estado = EstadoOrden.Pagada;
        EstadoPago = Enums.EstadoPago.Confirmado;
        PedidoLogisticoId = pedidoLogisticoId;
    }

    public void MarcarPagoFallido()
    {
        Estado = EstadoOrden.PagoFallido;
        EstadoPago = Enums.EstadoPago.Fallido;
    }

    public void MarcarCancelada()
    {
        Estado = EstadoOrden.Cancelada;
        EstadoPago = Enums.EstadoPago.Cancelado;
    }

    public void MarcarExpirada()
    {
        Estado = EstadoOrden.Cancelada;
        EstadoPago = Enums.EstadoPago.Expirado;
    }

    public void MarcarRetirada()
    {
        Estado = EstadoOrden.Retirada;
    }
}
