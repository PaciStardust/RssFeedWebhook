using RssFeedWebhook.Json;
using Serilog;
using Serilog.Core;
using System.Reflection;

namespace RssFeedWebhook
{
    internal class Program
    {
        internal static string MainDirectory { get; private set; } = string.Empty;
        internal static string ConfigDirectory { get; private set; } = string.Empty;
        internal static string ConfigFilepath { get; private set; } = string.Empty;
        internal static readonly Logger Logger = new LoggerConfiguration()
                .MinimumLevel.Debug().MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
                .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss.fff}] [{Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}")
                .Enrich.FromLogContext()
                .CreateLogger();

        static async Task Main(string[] args)
        {
            var log = Logger.ForContext<Program>();
            log.Information("RssFeedWebhook by Paci Stardust - Starting up");
            
            //Setting important directories
            MainDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? Directory.GetCurrentDirectory();
            ConfigDirectory = Path.Combine(MainDirectory, "cfg");
            ConfigFilepath = Path.GetFullPath(Path.Combine(ConfigDirectory, "config.json"));

            var config = Config.ReadFrom(ConfigFilepath);

            if (args.Contains("editor"))
            {
                if (config is null)
                {
                    config = Config.Create(ConfigFilepath);
                }
                else
                {
                    GuidedConfigCreator.DisplayFeedAndTemplateConfigurationScreen(config);
                }
                config.SaveAt(ConfigFilepath);
                return;
            }
            else
            {
                if (config is null)
                {
                    return;
                }
            }

            log.Information("Config has been loaded, webhook initializing...");
            var webhook = new RssFeedWebhook(config);
            await webhook.Start();
            await Task.Delay(-1);
        }
    }
}
