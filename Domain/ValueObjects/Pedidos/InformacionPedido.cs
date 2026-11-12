using Domain.Enums;
using Domain.Exceptions;

namespace Domain.ValueObjects;

public sealed class InformacionPedido
{
    private InformacionPedido() { }
    public string? RemitenteNombre { get; private set; }
    public string? RemitenteTelefono { get; private set; }
    public string? RemitenteCorreo { get; private set; }
    public string? DestinatarioCorreo { get; private set; }
    public string? Observaciones { get; private set; }
    public TipoCarga? TipoCarga { get; private set; }
    public decimal? PesoKg { get; private set; }
    public bool? Fragil { get; private set; }
    public PrioridadPedido? Prioridad { get; private set; }

    public static InformacionPedido Crear(string? nombre, string? telefono, string? correo,
        string? destinatarioCorreo, string? observaciones, TipoCarga? tipo, decimal? pesoKg,
        bool? fragil, PrioridadPedido? prioridad)
    {
        var n = Texto(nombre, 150); var t = Texto(telefono, 30);
        if ((n is null) != (t is null)) throw new DomainException("El remitente requiere nombre y teléfono juntos.");
        if (pesoKg is <= 0 or > 10000) throw new DomainException("El peso debe ser mayor que cero y no superar 10000 kg.");
        if (pesoKg.HasValue && decimal.Round(pesoKg.Value, 3) != pesoKg.Value)
            throw new DomainException("El peso admite como máximo tres decimales en kilogramos.");
        if (tipo.HasValue && !Enum.IsDefined(tipo.Value) || prioridad.HasValue && !Enum.IsDefined(prioridad.Value))
            throw new DomainException("Tipo de carga o prioridad inválidos.");
        return new() { RemitenteNombre = n, RemitenteTelefono = t, RemitenteCorreo = Correo(correo),
            DestinatarioCorreo = Correo(destinatarioCorreo), Observaciones = Texto(observaciones, 2000),
            TipoCarga = tipo, PesoKg = pesoKg, Fragil = fragil, Prioridad = prioridad };
    }
    private static string? Texto(string? v, int max)
    {
        if (string.IsNullOrWhiteSpace(v)) return null;
        if (v.Trim().Length > max) throw new DomainException($"El texto supera {max} caracteres.");
        return v.Trim();
    }
    private static string? Correo(string? v)
    {
        var texto = Texto(v, 254);
        if (texto is not null && (!System.Net.Mail.MailAddress.TryCreate(texto, out var correo) || correo.Address != texto))
            throw new DomainException("El correo electrónico no es válido.");
        return texto;
    }
}
