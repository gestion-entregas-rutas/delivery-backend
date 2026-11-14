using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

public sealed class PagoOrden : Entity
{
    private PagoOrden() { }
    public Guid OrdenId { get; private set; }
    public string Metodo { get; private set; } = "Simulado";
    public string? Referencia { get; private set; }
    public EstadoPago Estado { get; private set; }
    public DateTimeOffset CreadoEn { get; private set; }
    public DateTimeOffset? ConfirmadoEn { get; private set; }
    public DateTimeOffset? ExpiraEn { get; private set; }

    public static PagoOrden Crear(Guid ordenId, string metodo, string? referencia, EstadoPago estado, DateTimeOffset ahora, DateTimeOffset? expiraEn = null) =>
        new() { OrdenId = ordenId, Metodo = metodo, Referencia = referencia, Estado = estado, CreadoEn = ahora, ConfirmadoEn = estado == EstadoPago.Confirmado ? ahora : null, ExpiraEn = expiraEn };

    public void Confirmar(DateTimeOffset ahora)
    {
        Estado = EstadoPago.Confirmado;
        ConfirmadoEn = ahora;
    }

    public void MarcarFallido()
    {
        Estado = EstadoPago.Fallido;
    }

    public void Cancelar()
    {
        Estado = EstadoPago.Cancelado;
    }

    public void Expirar()
    {
        Estado = EstadoPago.Expirado;
    }
}
