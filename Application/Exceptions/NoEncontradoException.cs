namespace Application.Exceptions;

public class NoEncontradoException(string entidad, Guid id)
    : Exception($"{entidad} '{id}' no existe.");
