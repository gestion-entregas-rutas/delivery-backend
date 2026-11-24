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
        RuleFor(x => x.Latitud).Must(double.IsFinite).InclusiveBetween(-90, 90);
        RuleFor(x => x.Longitud).Must(double.IsFinite).InclusiveBetween(-180, 180);
        RuleFor(x => x.FechaEntrega).NotEqual(default(DateOnly));
        RuleFor(x => x).Must(x => x.CompromisoInicioEn.HasValue == x.CompromisoFinEn.HasValue)
            .WithMessage("Indique ambos extremos del intervalo comprometido.");
        RuleFor(x => x.CompromisoFinEn).GreaterThan(x => x.CompromisoInicioEn)
            .When(x => x.CompromisoInicioEn.HasValue && x.CompromisoFinEn.HasValue);
        RuleFor(x => x.TiempoServicioMinutos).InclusiveBetween(1, 240)
            .When(x => x.TiempoServicioMinutos.HasValue);
    }
}
