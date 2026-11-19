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

    public async Task<CapturaPayPal> CapturarAsync(string paypalOrderId, string requestId, CancellationToken ct)
    {
        var token = await ObtenerTokenAsync(ct);
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/v2/checkout/orders/{Uri.EscapeDataString(paypalOrderId)}/capture");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add("PayPal-Request-Id", requestId);
        request.Content = JsonContent.Create(new { });
        using var response = await http.SendAsync(request, ct);
        using var doc = await LeerRespuestaAsync(response, ct);
        var root = doc.RootElement;
        var unit = root.GetProperty("purchase_units").EnumerateArray().FirstOrDefault();
        var payee = unit.ValueKind == JsonValueKind.Undefined ? default : unit.GetProperty("payee");
        var payments = unit.ValueKind == JsonValueKind.Undefined ? default : unit.GetProperty("payments");
        var capture = payments.ValueKind == JsonValueKind.Undefined ? default : payments.GetProperty("captures").EnumerateArray().FirstOrDefault();
        var amount = capture.ValueKind == JsonValueKind.Undefined ? (decimal?)null : decimal.Parse(capture.GetProperty("amount").GetProperty("value").GetString()!, System.Globalization.CultureInfo.InvariantCulture);
        var currency = capture.ValueKind == JsonValueKind.Undefined ? null : capture.GetProperty("amount").GetProperty("currency_code").GetString();
        var merchant = payee.ValueKind == JsonValueKind.Undefined ? null : payee.GetProperty("merchant_id").GetString();
        var captureId = capture.ValueKind == JsonValueKind.Undefined ? null : capture.GetProperty("id").GetString();
        var captureStatus = capture.ValueKind == JsonValueKind.Undefined ? null : capture.GetProperty("status").GetString();
        return new(root.GetProperty("status").GetString() == "COMPLETED" && captureStatus == "COMPLETED" && merchant == _config.MerchantId, root.GetProperty("status").GetString() ?? "UNKNOWN", captureId, amount, currency, merchant);
    }

    private async Task<string> ObtenerTokenAsync(CancellationToken ct)
    {
        _config.Validar();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/oauth2/token");
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_config.ClientId}:{_config.ClientSecret}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["grant_type"] = "client_credentials" });
        using var response = await http.SendAsync(request, ct);
        using var doc = await LeerRespuestaAsync(response, ct);
        return doc.RootElement.GetProperty("access_token").GetString()!;
    }

    private static async Task<JsonDocument> LeerRespuestaAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            throw new ConflictoException($"PayPal rechazó la operación (HTTP {(int)response.StatusCode}).", "PAYPAL_ERROR");
        return JsonDocument.Parse(body);
    }
}
