using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Application.Exceptions;
using Application.Interfaces;
using Microsoft.Extensions.Options;

namespace Infrastructure.ExternalServices.Pagos;

public sealed class PayPalCheckoutService(HttpClient http, IOptions<PayPalOptions> options) : IPayPalService
{
    private readonly PayPalOptions _config = options.Value;

    public async Task<OrdenPayPalCreada> CrearOrdenAsync(Guid ordenId, string numeroOrden, decimal total, CancellationToken ct)
    {
        var token = await ObtenerTokenAsync(ct);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/v2/checkout/orders");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add("PayPal-Request-Id", $"create-{ordenId:N}");
        request.Content = JsonContent.Create(new
        {
            intent = "CAPTURE",
            purchase_units = new[] { new {
                reference_id = ordenId.ToString("N"), custom_id = ordenId.ToString("N"), invoice_id = numeroOrden,
                amount = new { currency_code = "USD", value = total.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) },
                payee = new { merchant_id = _config.MerchantId }
            } },
            application_context = new { return_url = _config.ReturnUrl, cancel_url = _config.CancelUrl, user_action = "PAY_NOW", shipping_preference = "NO_SHIPPING" }
        });
        using var response = await http.SendAsync(request, ct);
        using var doc = await LeerRespuestaAsync(response, ct);
        var root = doc.RootElement;
        var id = root.GetProperty("id").GetString();
        var approveLink = root.GetProperty("links").EnumerateArray()
            .FirstOrDefault(x => x.GetProperty("rel").GetString() is "approve" or "payer-action");
        var approve = approveLink.ValueKind == JsonValueKind.Undefined ? null : approveLink.GetProperty("href").GetString();
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(approve))
            throw new InvalidOperationException("PayPal no devolvió la orden ni el enlace de aprobación esperados.");
        return new(id, approve);
    }

    public async Task<CapturaPayPal> CapturarAsync(string paypalOrderId, Guid ordenId, decimal totalEsperado, string requestId, CancellationToken ct)
    {
        var token = await ObtenerTokenAsync(ct);

        using var consulta = new HttpRequestMessage(HttpMethod.Get, $"/v2/checkout/orders/{Uri.EscapeDataString(paypalOrderId)}");
        consulta.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var respuestaConsulta = await http.SendAsync(consulta, ct);
        using var ordenPayPal = await LeerRespuestaAsync(respuestaConsulta, ct);
        var ordenRoot = ordenPayPal.RootElement;
        var estadoOrden = ordenRoot.TryGetProperty("status", out var estadoJson) ? estadoJson.GetString() : null;
        var unidades = ordenRoot.TryGetProperty("purchase_units", out var unidadesJson) && unidadesJson.ValueKind == JsonValueKind.Array
            ? unidadesJson.EnumerateArray().ToArray()
            : [];
        if (unidades.Length != 1)
            throw new ConflictoException("La orden PayPal no contiene una unidad de compra válida.", "PAYPAL_ORDEN_INVALIDA");

        var unidad = unidades[0];
        var referenciaInterna = unidad.TryGetProperty("reference_id", out var refJson) ? refJson.GetString() : null;
        var customId = unidad.TryGetProperty("custom_id", out var customJson) ? customJson.GetString() : null;
        var payee = unidad.TryGetProperty("payee", out var payeeJson) ? payeeJson : default;
        var merchant = payee.ValueKind == JsonValueKind.Object && payee.TryGetProperty("merchant_id", out var merchantJson)
            ? merchantJson.GetString()
            : null;
        var importeUnidad = LeerImporte(unidad, "amount");
        var monedaUnidad = LeerMoneda(unidad, "amount");

        if (referenciaInterna != ordenId.ToString("N") || customId != ordenId.ToString("N") ||
            merchant != _config.MerchantId || importeUnidad != totalEsperado || monedaUnidad != "USD")
            throw new ConflictoException("La orden PayPal no coincide con la orden, el importe, la moneda o el vendedor esperados.", "PAYPAL_ORDEN_NO_COINCIDE");

        // Permite recuperar el resultado si PayPal completó la captura pero se perdió la respuesta anterior.
        if (estadoOrden == "COMPLETED")
            return LeerCaptura(ordenRoot, merchant);
        if (estadoOrden != "APPROVED")
            throw new ConflictoException("El comprador aún no ha aprobado el pago en PayPal.", "PAYPAL_PAGO_NO_APROBADO");

        using var request = new HttpRequestMessage(HttpMethod.Post, $"/v2/checkout/orders/{Uri.EscapeDataString(paypalOrderId)}/capture");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add("PayPal-Request-Id", requestId);
        request.Content = JsonContent.Create(new { });
        using var response = await http.SendAsync(request, ct);
        using var doc = await LeerRespuestaAsync(response, ct);
        return LeerCaptura(doc.RootElement, merchant);
    }

    private static CapturaPayPal LeerCaptura(JsonElement root, string? merchant)
    {
        var estado = root.TryGetProperty("status", out var estadoJson) ? estadoJson.GetString() ?? "UNKNOWN" : "UNKNOWN";
        if (!root.TryGetProperty("purchase_units", out var unidades) || unidades.ValueKind != JsonValueKind.Array)
            return new(false, estado, null, null, null, merchant);
        foreach (var unidad in unidades.EnumerateArray())
        {
            if (!unidad.TryGetProperty("payments", out var pagos) || !pagos.TryGetProperty("captures", out var capturas) || capturas.ValueKind != JsonValueKind.Array)
                continue;
            foreach (var captura in capturas.EnumerateArray())
            {
                var captureStatus = captura.TryGetProperty("status", out var statusJson) ? statusJson.GetString() : null;
                var id = captura.TryGetProperty("id", out var idJson) ? idJson.GetString() : null;
                return new(estado == "COMPLETED" && captureStatus == "COMPLETED", estado, id,
                    LeerImporte(captura, "amount"), LeerMoneda(captura, "amount"), merchant);
            }
        }
        return new(false, estado, null, null, null, merchant);
    }

    private static decimal? LeerImporte(JsonElement contenedor, string propiedad)
    {
        if (!contenedor.TryGetProperty(propiedad, out var importe) || !importe.TryGetProperty("value", out var valor)) return null;
        return decimal.TryParse(valor.GetString(), System.Globalization.NumberStyles.Number,
            System.Globalization.CultureInfo.InvariantCulture, out var resultado) ? resultado : null;
    }

    private static string? LeerMoneda(JsonElement contenedor, string propiedad)
        => contenedor.TryGetProperty(propiedad, out var importe) && importe.TryGetProperty("currency_code", out var moneda)
            ? moneda.GetString()
            : null;

    // El token OAuth de PayPal dura horas: pedirlo en cada operación añadía un viaje completo
    // a PayPal antes de crear o capturar la orden. Se reutiliza hasta un minuto antes de vencer.
    private static readonly SemaphoreSlim BloqueoToken = new(1, 1);
    private static (string Clave, string Token, DateTimeOffset Vence)? tokenEnCache;

    private async Task<string> ObtenerTokenAsync(CancellationToken ct)
    {
        _config.Validar();
        var clave = $"{http.BaseAddress}|{_config.ClientId}";
        if (tokenEnCache is { } c && c.Clave == clave && c.Vence > DateTimeOffset.UtcNow) return c.Token;
        await BloqueoToken.WaitAsync(ct);
        try
        {
            if (tokenEnCache is { } d && d.Clave == clave && d.Vence > DateTimeOffset.UtcNow) return d.Token;
            using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/oauth2/token");
            var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_config.ClientId}:{_config.ClientSecret}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
            request.Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["grant_type"] = "client_credentials" });
            using var response = await http.SendAsync(request, ct);
            using var doc = await LeerRespuestaAsync(response, ct);
            var token = doc.RootElement.GetProperty("access_token").GetString()!;
            var segundos = doc.RootElement.TryGetProperty("expires_in", out var exp) && exp.TryGetInt32(out var s) ? s : 0;
            if (segundos > 120) tokenEnCache = (clave, token, DateTimeOffset.UtcNow.AddSeconds(segundos - 60));
            return token;
        }
        finally { BloqueoToken.Release(); }
    }

    private static async Task<JsonDocument> LeerRespuestaAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            throw new ConflictoException($"PayPal rechazó la operación (HTTP {(int)response.StatusCode}).", "PAYPAL_ERROR");
        return JsonDocument.Parse(body);
    }
}
