using Application.Exceptions;
using Domain.Exceptions;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Api.Middleware;

/// <summary>
/// Traduce las excepciones del negocio a respuestas RFC 7807. Lo que no reconoce (errores inesperados)
/// pasa al manejador por defecto, que responde 500 sin exponer detalles internos.
/// </summary>
public sealed class ManejadorExcepciones(ILogger<ManejadorExcepciones> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext contexto, Exception excepcion, CancellationToken ct)
    {
        // Los mensajes de estas excepciones los escribimos nosotros para el usuario, así que son seguros de mostrar.
        var problema = excepcion switch
        {
            ValidationException v => ProblemaDeValidacion(v),
            NoEncontradoException => Crear(StatusCodes.Status404NotFound, "Recurso no encontrado", excepcion.Message),
            ConflictoException => Crear(StatusCodes.Status409Conflict, "Conflicto de concurrencia", excepcion.Message),
            TransicionInvalidaException => Crear(StatusCodes.Status409Conflict, "Transición de estado inválida", excepcion.Message),
            DomainException => Crear(StatusCodes.Status422UnprocessableEntity, "Regla de negocio incumplida", excepcion.Message),
            _ => null
        };

        if (problema is null) return false;

        logger.LogWarning(excepcion, "Solicitud rechazada ({Estado}): {Mensaje}", problema.Status, excepcion.Message);

        problema.Instance = contexto.Request.Path;
        contexto.Response.StatusCode = problema.Status!.Value;
        await contexto.Response.WriteAsJsonAsync(problema, ct);
        return true;
    }

    private static ProblemDetails Crear(int estado, string titulo, string detalle) =>
        new() { Status = estado, Title = titulo, Detail = detalle };

    private static ValidationProblemDetails ProblemaDeValidacion(ValidationException ex) => new(
        ex.Errors.GroupBy(e => e.PropertyName)
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).Distinct().ToArray()))
    {
        Status = StatusCodes.Status400BadRequest,
        Title = "Datos inválidos",
        Detail = "Uno o más campos no cumplen las validaciones."
    };
}
