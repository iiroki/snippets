using System.Text.Json;

namespace Snippets.Serialization;

public static class JsonExtensions
{
    /// <summary>
    /// Deserializes a nullable JSON element into a string.
    /// </summary>
    public static string? Deserialize(this JsonElement? json, JsonSerializerOptions? opt = null) =>
        json.HasValue ? JsonSerializer.Serialize(json.Value, opt) : null;
}
