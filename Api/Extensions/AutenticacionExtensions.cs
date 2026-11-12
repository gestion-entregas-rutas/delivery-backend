using System.Text;
using Application.Interfaces;
using Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
namespace Api.Extensions;

public static class AutenticacionExtensions
{
    public static IServiceCollection AddAutenticacion(this IServiceCollection services, IConfiguration config)
    {
        var key = config["Jwt:Key"];
        if (string.IsNullOrWhiteSpace(key) || Encoding.UTF8.GetByteCount(key) < 32)
            throw new InvalidOperationException("Configure Jwt:Key con al menos 32 bytes aleatorios en secretos o variables de entorno.");
        if (string.IsNullOrWhiteSpace(config["Jwt:Issuer"]) || string.IsNullOrWhiteSpace(config["Jwt:Audience"]))
            throw new InvalidOperationException("Configure Jwt:Issuer y Jwt:Audience.");
        services.AddSingleton<EmisorJwt>();
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
        {
            o.MapInboundClaims = false;
            o.TokenValidationParameters = new()
            {
                ValidateIssuer = true, ValidIssuer = config["Jwt:Issuer"],
                ValidateAudience = true, ValidAudience = config["Jwt:Audience"],
                ValidateLifetime = true, ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
                ValidAlgorithms = [SecurityAlgorithms.HmacSha256], ClockSkew = TimeSpan.FromSeconds(30),
                NameClaimType = "name", RoleClaimType = "role"
            };
            o.Events = new JwtBearerEvents
            {
                OnMessageReceived = ctx =>
                {
                    if (ctx.Request.Path.StartsWithSegments("/hubs/seguimiento") &&
                        ctx.Request.Query.TryGetValue("access_token", out var token)) ctx.Token = token;
                    return Task.CompletedTask;
                },
                OnTokenValidated = async ctx =>
                {
                    if (!Guid.TryParse(ctx.Principal?.FindFirst("sub")?.Value, out var id)) { ctx.Fail("Sesión inválida."); return; }
                    var db = ctx.HttpContext.RequestServices.GetRequiredService<IAppDbContext>();
                    var u = await db.Usuarios.AsNoTracking().SingleOrDefaultAsync(u => u.Id == id, ctx.HttpContext.RequestAborted);
                    if (u is null || !u.Activo || u.SelloSeguridad.ToString() != ctx.Principal!.FindFirst("sello")?.Value)
                        ctx.Fail("Sesión revocada.");
                }
            };
        });
        services.AddAuthorization(o =>
        {
            var completo = new AuthorizationPolicyBuilder().RequireAuthenticatedUser()
                .RequireClaim("cambio_pendiente", "false").Build();
            o.DefaultPolicy = completo; o.FallbackPolicy = completo;
            o.AddPolicy("Completo", completo);
            o.AddPolicy("Identificado", p => p.RequireAuthenticatedUser());
            o.AddPolicy("Administrador", p => p.RequireAuthenticatedUser()
                .RequireClaim("cambio_pendiente", "false").RequireRole("Administrador"));
        });
        return services;
    }
}
