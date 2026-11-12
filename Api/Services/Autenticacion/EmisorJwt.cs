using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Application.DTOs;
using Application.Mappings;
using Domain.Entities;
using Microsoft.IdentityModel.Tokens;
namespace Api.Services;

public sealed class EmisorJwt(IConfiguration config, TimeProvider reloj)
{
    public SesionDto Crear(Usuario u)
    {
        var ahora = reloj.GetUtcNow();
        var expira = ahora.AddMinutes(u.DebeCambiarCredenciales ? 10 : 30);
        var claims = new List<Claim>
        {
            new("sub", u.Id.ToString()), new("name", u.NombreUsuario), new("role", u.Rol.ToString()),
            new("sello", u.SelloSeguridad.ToString()),
            new("cambio_pendiente", u.DebeCambiarCredenciales ? "true" : "false"),
            new("jti", Guid.NewGuid().ToString())
        };
        if (u.RepartidorId.HasValue) claims.Add(new("repartidor_id", u.RepartidorId.Value.ToString()));
        var token = new JwtSecurityToken(config["Jwt:Issuer"], config["Jwt:Audience"], claims,
            ahora.UtcDateTime, expira.UtcDateTime, new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Key"]!)), SecurityAlgorithms.HmacSha256));
        return new(new JwtSecurityTokenHandler().WriteToken(token), expira, u.ToDto());
    }
}
