using Application.DTOs;
using FluentValidation;

namespace Application.Validators;

public class ArmarRutasValidator : AbstractValidator<ArmarRutasRequest>
{
    public ArmarRutasValidator()
    {
        RuleFor(x => x.Fecha).NotEqual(default(DateOnly));
    }
}
