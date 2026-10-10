using Application.DTOs;
using FluentValidation;

namespace Application.Validators;

public class ActualizarZonaValidator : AbstractValidator<ActualizarZonaRequest>
{
    public ActualizarZonaValidator()
    {
        RuleFor(x => x.Nombre).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Poligono).NotNull().Must(p => p.Count >= 3)
            .WithMessage("El polígono necesita al menos 3 vértices.");
        RuleFor(x => x.MaxPedidosPorRuta).GreaterThanOrEqualTo(1);
    }
}
