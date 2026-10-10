
namespace Application.DTOs;

public sealed record PaginaDto<T>(IReadOnlyList<T> Items, int Total, int Pagina, int Tamano);
