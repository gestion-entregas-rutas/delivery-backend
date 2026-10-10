using Application.DTOs;
using FluentValidation;

namespace Application.Validators;

public class CrearRepartidorValidator : AbstractValidator<CrearRepartidorRequest>
{
    public CrearRepartidorValidator()
    {
        RuleFor(x => x.Nombre).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Telefono).NotEmpty().MaximumLength(30);
        RuleFor(x => x.CapacidadMaxima).GreaterThanOrEqualTo(1);
    }
}
