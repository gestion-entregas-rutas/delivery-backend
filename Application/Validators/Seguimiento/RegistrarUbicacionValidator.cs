using Application.DTOs;
using FluentValidation;
namespace Application.Validators;
public sealed class RegistrarUbicacionValidator : AbstractValidator<RegistrarUbicacionRequest>
{
    public RegistrarUbicacionValidator()
    {
        RuleFor(x => x.RutaId).NotEmpty();
        RuleFor(x => x.Latitud).Must(double.IsFinite).InclusiveBetween(-90, 90);
        RuleFor(x => x.Longitud).Must(double.IsFinite).InclusiveBetween(-180, 180);
        RuleFor(x => x.PrecisionMetros).Must(double.IsFinite).InclusiveBetween(0, 1000);
        RuleFor(x => x.CapturadaEn).NotEqual(default(DateTimeOffset));
    }
}
