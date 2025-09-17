using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Archivist.Models;

public class PythonInputData
{
    [JsonPropertyName("command")]
    public string Command { get; set; } = "start";

    [JsonPropertyName("files")]
    public List<FileData> Files { get; set; } = new();

    [JsonPropertyName("vault")]
    public string Vault { get; set; } = string.Empty;

    [JsonPropertyName("subdirectory")]
    public string Subdirectory { get; set; } = string.Empty;

    [JsonPropertyName("format")]
    public string Format { get; set; } = string.Empty;
}

public class FileData
{
    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    [JsonPropertyName("character")]
    public string Character { get; set; } = string.Empty;
}