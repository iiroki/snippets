using System.Text.Json;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Snippets.Database;

public class JsonStringConverter(JsonSerializerOptions? opt = null)
    : ValueConverter<JsonElement, string>(x => ConvertToString(x, opt), x => ConvertToJson(x, opt))
{
    private static string ConvertToString(JsonElement value, JsonSerializerOptions? opt = null) =>
        value.ValueKind != JsonValueKind.Undefined ? JsonSerializer.Serialize(value, opt) : string.Empty;

    private static JsonElement ConvertToJson(string? value, JsonSerializerOptions? opt = null) =>
        !string.IsNullOrEmpty(value) ? JsonSerializer.Deserialize<JsonElement>(value, opt) : default;
}
