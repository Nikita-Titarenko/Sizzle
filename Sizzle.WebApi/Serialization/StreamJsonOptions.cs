using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sizzle.WebApi.Serialization;

internal static class StreamJsonOptions
{
    internal static readonly JsonSerializerOptions Value = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };
}