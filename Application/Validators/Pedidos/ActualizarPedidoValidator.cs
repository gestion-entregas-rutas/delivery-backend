using Application.DTOs;
using FluentValidation;
namespace Application.Validators;

public sealed class ActualizarPedidoValidator : AbstractValidator<ActualizarPedidoRequest>
{
    public ActualizarPedidoValidator()
    {
        RuleFor(x => new RegistrarPedidoRequest(x.ClienteNombre, x.ClienteTelefono, x.Direccion,
            x.Latitud, x.Longitud, x.FechaEntrega, x.HoraEntrega, x.ZonaId, x.Detalles))
            .SetValidator(new RegistrarPedidoValidator());
    }
}
