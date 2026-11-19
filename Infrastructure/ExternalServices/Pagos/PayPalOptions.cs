namespace Infrastructure.ExternalServices.Pagos;

public sealed class PayPalOptions
{
    public const string Section = "PayPal";
    public bool Habilitado { get; init; }
    public string Entorno { get; init; } = "Sandbox";
    public string? ClientId { get; init; }
    public string? ClientSecret { get; init; }
    public string? MerchantId { get; init; }
    public string? ReturnUrl { get; init; }
    public string? CancelUrl { get; init; }

    public void Validar()
    {
        if (!Habilitado || Entorno != "Sandbox" || string.IsNullOrWhiteSpace(ClientId) ||
            string.IsNullOrWhiteSpace(ClientSecret) || string.IsNullOrWhiteSpace(MerchantId) ||
            !Uri.TryCreate(ReturnUrl, UriKind.Absolute, out _) || !Uri.TryCreate(CancelUrl, UriKind.Absolute, out _))
            throw new InvalidOperationException("PayPal Sandbox no está habilitado o su configuración está incompleta.");
    }
}
