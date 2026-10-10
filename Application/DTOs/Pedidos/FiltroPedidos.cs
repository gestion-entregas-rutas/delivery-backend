using Domain.Enums;

namespace Application.DTOs;

public sealed record FiltroPedidos(
    DateOnly? Fecha = null,
    Guid? ZonaId = null,
    EstadoPedido? Estado = null,
    int Pagina = 1,
    int Tamano = 50);
