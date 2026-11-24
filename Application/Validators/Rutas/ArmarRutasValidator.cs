using Application.DTOs;
using FluentValidation;

namespace Application.Validators;

public class ArmarRutasValidator : AbstractValidator<ArmarRutasRequest>
{
    public ArmarRutasValidator()
    {
        RuleFor(x => x.Fecha).NotEqual(default(DateOnly));
        RuleFor(x => x.Origen).NotNull().When(x => x.SalidaProgramadaEn.HasValue);
        When(x => x.Origen is not null, () =>
        {
            RuleFor(x => x.Origen!.Latitud).Must(double.IsFinite).InclusiveBetween(-90, 90);
            RuleFor(x => x.Origen!.Longitud).Must(double.IsFinite).InclusiveBetween(-180, 180);
        });
    }
}
