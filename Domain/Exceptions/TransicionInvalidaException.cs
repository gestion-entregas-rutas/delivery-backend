namespace Domain.Exceptions;

/// <summary>Se lanza cuando se intenta un cambio de estado que el flujo no permite.</summary>
public class TransicionInvalidaException(string entidad, object desde, object hacia)
    : DomainException($"{entidad}: no se puede pasar de '{desde}' a '{hacia}'.");
