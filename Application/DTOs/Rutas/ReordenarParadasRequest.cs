namespace Application.DTOs;
/// <summary>Lista completa de ids de pedidos en el orden deseado; Version es la última versión de la ruta.</summary>
public sealed record ReordenarParadasRequest(IReadOnlyList<Guid> PedidosEnOrden, uint Version);
