using System.Text.Json.Serialization;

namespace Archivist.Models;

public class ProgressMessage
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("stage")]
    public string Stage { get; set; } = string.Empty;

    [JsonPropertyName("percentage")]
    public int Percentage { get; set; }
}
