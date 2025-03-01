namespace RssFeedWebhook.Json
{
    internal class Feed
    {
        public string Template {  get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public long LastPublished { get; set; } = DateTimeOffset.Now.ToUnixTimeSeconds();
    }
}
