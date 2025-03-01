using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RssFeedWebhook.Json
{
    internal class Template
    {
        public string? EmptyText {get; set;}
        public string? Content {get; set;}
        public List<string>? Embeds {get; set;}
    }
}
