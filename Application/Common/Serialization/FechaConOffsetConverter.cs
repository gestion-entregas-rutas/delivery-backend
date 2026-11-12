using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
namespace Application.Common;

/// <summary>Se usa solo en capturas GPS: evita interpretar una hora del teléfono sin zona horaria.</summary>
public sealed class FechaConOffsetConverter : JsonConverter<DateTimeOffset>
{
    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
    {
        var texto = reader.TokenType == JsonTokenType.String ? reader.GetString() : null;
        if (texto is null || !Regex.IsMatch(texto, @"(Z|[+-]\d{2}:\d{2})$", RegexOptions.CultureInvariant) ||
            !DateTimeOffset.TryParse(texto, CultureInfo.InvariantCulture, DateTimeStyles.None, out var fecha))
            throw new JsonException("La captura GPS requiere fecha ISO 8601 con Z u offset explícito.");
        return fecha.ToUniversalTime();
    }
    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToUniversalTime());
}
