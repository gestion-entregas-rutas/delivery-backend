namespace Application.DTOs;
public sealed record SesionDto(string AccessToken, DateTimeOffset ExpiraEn, UsuarioDto Usuario, string TokenType = "Bearer");
