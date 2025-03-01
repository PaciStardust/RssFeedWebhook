using RssFeedWebhook.Json;
using Serilog;
using System.ServiceModel.Syndication;
using System.Text;
using System.Xml;

namespace RssFeedWebhook
{
    internal class RssFeedWebhook
    {
        private readonly HttpClient _client;
        private readonly Dictionary<string, TemplateApplicator> _templateApplicators;
        private readonly Config _config;
        private System.Timers.Timer? _timer;
        private readonly ILogger _logger;

        public RssFeedWebhook(Config config)
        {
            _config = config;
            _templateApplicators = LoadTemplates(config.Templates);
            _client = new();
            _logger = Program.Logger.ForContext<RssFeedWebhook>();
        }

        private static Dictionary<string, TemplateApplicator> LoadTemplates(Dictionary<string, Template> templates)
        {
            return templates.Select(x => new KeyValuePair<string, TemplateApplicator>(x.Key, new(x.Value))).ToDictionary()!;
        }

        public async Task Start()
        {
            _logger.Information("Running logic once on startup");
            Stop();
            await RunLogic();

            var timer = new System.Timers.Timer()
            {
                AutoReset = true,
                Interval = _config.IntervalMinutes * 60 * 1000
            };
            timer.Elapsed += async (_,_) => await RunLogic();

            _logger.Information("Starting logic loop at interval {Interval}ms", timer.Interval);
            timer.Start();
            _timer = timer;
        }

        public void Stop()
        {
            _timer?.Stop();
            _timer?.Dispose();
            _timer = null;
        }

        private async Task RunLogic()
        {
            _logger.Information("Running logic...");
            var toProcess = GetItemsToProcess();
            await Process(toProcess);
        }

        private List<(Feed, List<SyndicationItem>)> GetItemsToProcess()
        {
            _logger.Information("Getting items to process...");
            var toProcess = new List<(Feed, List<SyndicationItem>)>();
            foreach (var (name, feed) in _config.Feeds)
            {
                _logger.Information("Loading feed {feed}", feed.Url);
                using var xmlReader = XmlReader.Create(feed.Url);
                var syndicationFeed = SyndicationFeed.Load(xmlReader);

                _logger.Debug("Adding items from feed {feed}", feed.Url);
                var newPosts = new List<SyndicationItem>();
                foreach (var item in syndicationFeed.Items)
                {
                    var date = item.LastUpdatedTime > DateTimeOffset.MinValue
                        ? item.LastUpdatedTime
                        : item.PublishDate;

                    if (date.ToUnixTimeSeconds() <= feed.LastPublished)
                        break;
                    item.SourceFeed = syndicationFeed;
                    newPosts.Add(item);
                }
                toProcess.Add((feed, newPosts));
            }
            return toProcess;
        }

        private async Task Process(List<(Feed, List<SyndicationItem>)> toProcess)
        {
            _logger.Information("Processing items...");
            foreach (var (feed, items) in toProcess)
            {
                for (var i = items.Count - 1; i > -1; i--)
                {
                    var item = items[i];
                    _logger.Debug("Applying template {Template} to item {Item}", feed.Template, item.Id);
                    var message = _templateApplicators[feed.Template].Apply(item);
                    var res = await Send(message);
                    if (!res)
                    {
                        _logger.Warning("Failed to send message for item {Item}, all others of feed are skipped", item.Id);
                        break;
                    }
                    await Task.Delay(_config.SendTimeoutMs);
                    var date = item.LastUpdatedTime > DateTimeOffset.MinValue
                        ? item.LastUpdatedTime
                        : item.PublishDate;
                    if (date.ToUnixTimeSeconds() > feed.LastPublished)
                    {
                        feed.LastPublished = date.ToUnixTimeSeconds();
                        _logger.Debug("Updating config after sending item {Item}", item.Id);
                        var sRes = _config.SaveAt(Program.ConfigFilepath);
                        if (!sRes)
                        {
                            _logger.Warning("Failed to save config after sending message for item {Item}, it may repeat", item.Id);
                        }
                    }
                }
            }
        }

        private async Task<bool> Send(string message)
        {
            var request = new StringContent(message, Encoding.UTF8, "application/json");
            using var ctSource = new CancellationTokenSource(30000);
            try
            {
                _logger.Debug("Sending Message {Message}", message);
                var res = await _client.PostAsync(_config.WebhookUrl, request, ctSource.Token);
                var resTxt = await res.Content.ReadAsStringAsync();
                if (!res.IsSuccessStatusCode)
                {
                    _logger.Error("Request {Message} failed with code {Code}: {Response}", message, res.StatusCode, resTxt);
                    return false;
                }
                _logger.Debug("Received response {Response} for message {Message}", resTxt, message);
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Request {Message} failed", message);
            }
            return false;
        }
    }
}
