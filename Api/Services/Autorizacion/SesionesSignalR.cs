using System.Collections.Concurrent;
using Application.Interfaces;
using Microsoft.AspNetCore.SignalR;
namespace Api.Services;

// El sello invalida los JWT por HTTP; este registro cierra también los sockets ya abiertos.
public sealed class SesionesSignalR : IRevocadorSesiones
{
    private readonly ConcurrentDictionary<string, (Guid UsuarioId, HubCallerContext Contexto)> conexiones = new();
    public void Registrar(Guid id, HubCallerContext contexto) => conexiones[contexto.ConnectionId] = (id, contexto);
    public void Quitar(string conexionId) => conexiones.TryRemove(conexionId, out _);
    public void Revocar(Guid usuarioId)
    {
        foreach (var c in conexiones.Where(c => c.Value.UsuarioId == usuarioId))
        {
            c.Value.Contexto.Abort(); conexiones.TryRemove(c.Key, out _);
        }
    }
}
