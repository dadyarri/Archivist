using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Windows.ApplicationModel;

namespace Archivist.Models
{
    public record AppConfig
    {
        [JsonIgnore]
        private static string _configDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Archivist");
        [JsonIgnore]
        private static string _configPath = Path.Combine(_configDir, "config.json");
        public string Vault { get; set; } = string.Empty;
        public string SubDirectory { get; set; } = string.Empty;
        public string Format { get; set; } = string.Empty;

        public async Task SaveAsync()
        {
            var json = JsonSerializer.Serialize(this);
            await File.WriteAllTextAsync(_configPath, json);
        }

        public static AppConfig Load()
        {
            Directory.CreateDirectory(_configDir);
            if (File.Exists(_configPath))
            {
                var json = File.ReadAllText(_configPath);
                return JsonSerializer.Deserialize<AppConfig>(json)!;
            }
            else
            {
                return new AppConfig();
            }
        }


    }
}