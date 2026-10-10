using Application.DTOs;
using FluentValidation;

namespace Application.Validators;

public class RegistrarPedidoValidator : AbstractValidator<RegistrarPedidoRequest>
{
    public RegistrarPedidoValidator()
    {
        RuleFor(x => x.ClienteNombre).NotEmpty().MaximumLength(150);
        RuleFor(x => x.ClienteTelefono).NotEmpty().MaximumLength(30);
        RuleFor(x => x.Direccion).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Latitud).InclusiveBetween(-90, 90);
        RuleFor(x => x.Longitud).InclusiveBetween(-180, 180);
        RuleFor(x => x.FechaEntrega).NotEqual(default(DateOnly));
    }
}
