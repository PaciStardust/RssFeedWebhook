using Serilog;
using System.Text.Json;

namespace RssFeedWebhook.Json
{
    internal class Config
    {
        public string WebhookUrl { get; set; } = string.Empty;
        public int IntervalMinutes { get; set; } = 15;
        public int SendTimeoutMs { get; set; } = 500;
        public Dictionary<string, Feed> Feeds { get; set; } = [];
        public Dictionary<string, Template> Templates { get; set; } = [];

        private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };
        private static readonly ILogger _logger = Program.Logger.ForContext<Config>();

        public bool SaveAt(string path)
        {
            try
            {
                _logger.Debug("Saving config to {Path}", path);
                File.WriteAllText(path, JsonSerializer.Serialize(this ?? new(), _jsonOptions));
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to save config to {Path}", path);
                return false;
            }
        }

        public static Config? ReadFrom(string path)
        {
            _logger.Information("Attempting to load config from {Path}", path);
            try
            {
                var directory = Path.GetDirectoryName(path);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory!);
                }

                if (File.Exists(path))
                {
                    var fileContents = File.ReadAllText(path);
                    return JsonSerializer.Deserialize<Config>(fileContents)!;
                }
                else
                {
                    _logger.Error("Unable to find config file at {Path}", path);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Config file at {Path} could not be loaded\n\nPlease fix or delete the file or proceed to guided creation", path);
            }
            return null;
        }

        public static Config Create(string path)
        {
            _logger.Information("Starting guided config creator");
            var config = GuidedConfigCreator.CreateConfig();
            return config;
        }

    }
}
