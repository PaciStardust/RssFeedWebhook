using RssFeedWebhook.Json;
using System.ServiceModel.Syndication;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml;

namespace RssFeedWebhook
{
    internal static class GuidedConfigCreator 
    {
        private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

        public static Config CreateConfig()
        {
            Console.WriteLine("RssFeedWebhook by Paci Stardust - Init\n");

            var config = new Config()
            {
                WebhookUrl = AskString("What is the URL of your Discord Webhook?\n(Webhooks are managed in a Discord server under Settings > Integrations > Webhooks)"),
                Feeds = new()
                {
                    {
                        "Example Bsky Paci Stardust", new()
                        {
                            LastPublished = DateTimeOffset.Now.ToUnixTimeSeconds(),
                            Template = "Example Bsky",
                            Url = "https://bsky.app/profile/did:plc:vdxevqmw5aqc46lem37auvmg/rss"
                        }
                    }
                },
                Templates = new() {
                    {
                        "Example Bsky", new()
                        {
                            Content = "New post from \\\"[$FeedTitle$]\\\"[$ItemAuthorsSmart[?' '][<'by ']$]at <t:[$ItemTimeSmartUnix$]:f>[$ItemLinks[?''][<':\\nhttps://bsky.app']$]",
                            EmptyText = "[MISSING]"
                        }
                    }
                }
            };
            DisplayFeedAndTemplateConfigurationScreen(config);
            return config;
        }

        #region Screens
        public static void DisplayFeedAndTemplateConfigurationScreen(Config config)
        {
            var text = "Feed and Template Configuration";
            while (true)
            {
                (string, string)[] options = [
                    ("F", $"Feeds ({config.Feeds.Count})"),
                    ("T", $"Templates ({config.Templates.Count})"),
                    ("X", "Exit")
                ];

                switch (CreateFullMenu(text, options))
                {
                    case "F":
                        DisplayConfigurationScreen(config, false);
                        break;

                    case "T":
                        DisplayConfigurationScreen(config, true);
                        break;

                    case "X":
                        return;
                }
            }
        }

        private static void DisplayConfigurationScreen(Config config, bool template)
        {
            var insertWord = template ? "Template" : "Feed";

            while (true)
            {
                var configList = template
                    ? string.Join("\n", config.Templates.Select(x => $" - {x.Key}"))
                    : string.Join("\n", config.Feeds.Select(x => $" - {x.Key}: {x.Value.Template} (Last {DateTimeOffset.FromUnixTimeSeconds(x.Value.LastPublished)})"));

                var text = $"{insertWord} Configuration\n\n{(string.IsNullOrWhiteSpace(configList) ? $"[No {insertWord}s to list]" : configList)}";

                (string, string)[] options = [
                    ("E", $"Edit/Create/View {insertWord}"),
                    ("D", $"Delete {insertWord}"),
                    ("X", "Exit")
                ];

                switch (CreateFullMenu(text, options))
                {
                    case "E":
                        var creationName = AskString($"{insertWord} Name?");
                        if (template)
                        {
                            DisplayTemplateEditor(config, creationName);
                        }
                        else
                        {
                            DisplayFeedEditor(config, creationName);
                        }
                        break;

                    case "D":
                        var deletionName = AskString($"{insertWord} Name?");
                        if (template)
                        {
                            config.Templates.Remove(deletionName);
                        }
                        else
                        {
                            config.Feeds.Remove(deletionName);
                        }
                        break;

                    case "X":
                        return;
                }
            }
        }

        private static void DisplayFeedEditor(Config config, string feedName)
        {
            if (!config.Feeds.ContainsKey(feedName))
                config.Feeds[feedName] = new();
            var feed = config.Feeds[feedName];

            while (true)
            {
                bool isTemplateValid = !string.IsNullOrWhiteSpace(feedName) && config.Templates.ContainsKey(feed.Template);

                var text = $"Editing Feed \"{feedName}\"\n\nUrl: {(string.IsNullOrWhiteSpace(feed.Url) ? "[NONE]" : feed.Url)}\nTemplate: {(string.IsNullOrWhiteSpace(feed.Template) ? "[NONE]" : feed.Template)}{(isTemplateValid ? string.Empty : " (Template not found)")}";
                (string, string)[] options = [
                    ("U", "Set URL"),
                    ("T", "Set Template"),
                    ("X", "Exit")
                ];

                switch (CreateFullMenu(text, options))
                {
                    case "U":
                        var newUrl = AskString("What is the new Url?");
                        feed.Url = newUrl;
                        break;

                    case "T":
                        var templateList = string.Join(", ", config.Templates.Keys);
                        var newTemplate = AskString($"Which template would you like to use?\n\n{(string.IsNullOrWhiteSpace(templateList) ? "Warning: There are currently no templates available!" : $"Available Templates:\n{templateList}")}\n");
                        var save = true;
                        while (!config.Templates.ContainsKey(newTemplate))
                        {
                            var answer = AskString("Warning: The chosen template could not be found in the template list, a feed can not be used without a valid template\nType \"OK\" to still set the template or \"CANCEL\" to cancel");
                            if (answer.Equals("OK", StringComparison.OrdinalIgnoreCase))
                            {
                                break;
                            }
                            if (answer.Equals("CANCEL", StringComparison.OrdinalIgnoreCase))
                            {
                                save = false;
                                break;
                            }
                        }
                        if (save)
                        {
                            feed.Template = newTemplate;
                        }
                        break;

                    case "X":
                        return;
                }
            }
        }

        private static void DisplayTemplateEditor(Config config, string templateName)
        {
            if (!config.Templates.ContainsKey(templateName))
                config.Templates[templateName] = new();
            var template = config.Templates[templateName];

            while (true)
            {
                var sb = new StringBuilder($"Editing Template \"{templateName}\"\n\n");
                sb.AppendLine($"Content: {(string.IsNullOrWhiteSpace(template.Content) ? "[NONE]" : template.Content)}");
                sb.AppendLine($"Empty Replacement: {(string.IsNullOrWhiteSpace(template.EmptyText) ? "[NONE]" : template.EmptyText)}");
                var embedIndex = 0;
                var embedList = string.Join("\n", template.Embeds?.Select(x => $" {embedIndex++}: {x}") ?? []);
                sb.Append($"Embeds:{(string.IsNullOrWhiteSpace(embedList) ? " [NONE]" : "\n" + embedList)}");

                (string, string)[] options = [
                    ("C", "Set Content"),
                    ("E", $"Set List of Embeds ({template.Embeds?.Count ?? 0})"),
                    ("R", "Set Empty Replacement (Placeholder if text is empty)"),
                    ("P", "List Placeholder Tokens"),
                    ("V", "Verify Template"),
                    ("X", "Exit")
                ];

                switch (CreateFullMenu(sb.ToString(), options))
                {
                    case "C":
                        template.Content = DisplayReplacementTokenTextEditor(templateName, "Content", template.Content ?? string.Empty);
                        break;

                    case "E":
                        DisplayTemplateEmbedEditor(config, templateName);
                        break;

                    case "R":
                        var newEmptyReplacement = AskString("What is the new Empty Replacement?");
                        template.EmptyText = newEmptyReplacement;
                        break;

                    case "P":
                        Console.WriteLine($"\nAll Placeholder Tokens:\n\n{TemplateApplicator.TokenDescription}\n\nAll Placeholder Token Modifiers:\n\n{TemplateApplicator.TokenModifierDescription}");
                        AskString("A list of all tokens will be displayed when editing the template, press enter to continue", true);
                        break;

                    case "V":
                        var testApplicator = new TemplateApplicator(template);
                        if (testApplicator.IsEmpty())
                        {
                            AskString("The template is empty and can not be used, press enter to cancel", true);
                        }
                        var testUrl = AskString("What feed URL what do you want this template to be tested against?");
                        try
                        {
                            using var testReader = XmlReader.Create(testUrl);
                            var testFeed = SyndicationFeed.Load(testReader);
                            var testItem = testFeed.Items.FirstOrDefault() ?? throw new Exception("Feed did not contain any items to read");
                            testItem.SourceFeed = testFeed;
                            var output = testApplicator.Apply(testItem);
                            var error = string.Empty;
                            try
                            {
                                var obj = JsonNode.Parse(output);
                                output = obj!.ToJsonString(_jsonOptions);
                            }
                            catch (Exception ex)
                            {
                                error = ex.ToString();
                            }
                            AskString($"\nResult of template using newest item from feed:\n\n{output}\n\n{(string.IsNullOrWhiteSpace(error) ? string.Empty : $"Could not format as JSON was not valid: {error}\n\n")}Press enter to continue", true);
                        }
                        catch (Exception ex)
                        {
                            AskString($"Unable to load feed: \"{ex.Message}\"\nPress enter to cancel", true);
                            break;
                        }
                        break;

                    case "X":
                        return;
                }
            }
        }

        private static string DisplayReplacementTokenTextEditor(string templateName, string textType, string currentText, bool embed = false)
        {
            var title = $"Placeholder Token Text Editor - {templateName} ({textType})";
            var text = $"{title}\n\nCurrent Text:\n{(string.IsNullOrWhiteSpace(currentText) ? "[NONE]" : currentText)}\n\nPlaceholder Tokens:\n{TemplateApplicator.TokenList}\n\nPlaceholder Token Modifiers:\n{TemplateApplicator.TokenModifierList}\n\nFor more information on how replacement tokens work, press \"P\" in the template editor\n";
            Console.Clear();
            Console.WriteLine(text);
            while (true)
            {
                var input = AskString("What should the new text be?");
                if (embed)
                {
                    var validationResult = ValidateJson(input);
                    if (validationResult is not null)
                    {
                        var verifyValidate = AskString($"JSON verification failed:\n\n{validationResult.Message}\n\nDo you wish to proceed? (Type \"Y\" to confirm)");
                        if (!verifyValidate.Equals("Y", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }
                    }
                }
                var verify = AskString("Are you sure? (Type \"Y\" to confirm)");
                if (verify.Equals("Y", StringComparison.OrdinalIgnoreCase))
                {
                    return input;
                }
            }
        }

        private static void DisplayTemplateEmbedEditor(Config config, string templateName)
        {
            while (true)
            {
                var embedCount = config.Templates[templateName].Embeds?.Count ?? 0;
                var embedIndex = 0;
                var embedsText = embedCount == 0
                    ? " [NONE]"
                    : "\n" + string.Join("\n", config.Templates[templateName].Embeds!.Select(x => $" {embedIndex++}: {(string.IsNullOrWhiteSpace(x) ? "[NONE]" : (ValidateJson(x) is null ? string.Empty : "[INVALID] ") + x)}"));
                var fullText = $"Editing Embeds For Template \"{templateName}\"\n\nEmbeds:{embedsText}";

                (string, string)[] options = [
                    ("E", "Edit Text"),
                    ("M", "Move Text"),
                    ("C", "Create Text"),
                    ("X", "Exit")
                ];

                switch (CreateFullMenu(fullText, options))
                {
                    case "E":
                        if (embedCount == 0)
                        {
                            AskString("There are no positions to edit, press enter to cancel");
                            break;
                        }
                        var setIndex = AskInt("Position of text to set?", 0, embedCount-1);
                        config.Templates[templateName].Embeds![setIndex] = DisplayReplacementTokenTextEditor(templateName, $"Embed {setIndex}", config.Templates[templateName].Embeds![setIndex], true);
                        break;

                    case "M":
                        if (embedCount == 0)
                        {
                            AskString("There are no positions to edit, press enter to cancel", true);
                            break;
                        }
                        var moveOriginIndex = AskInt("Position of text to move?", 0, embedCount - 1);
                        var moveTargetIndex = AskInt("Target position?", 0, embedCount - 1);
                        var moveText = config.Templates[templateName].Embeds![moveOriginIndex];
                        config.Templates[templateName].Embeds!.RemoveAt(moveOriginIndex);
                        config.Templates[templateName].Embeds!.Insert(moveTargetIndex, moveText);
                        break;

                    case "C":
                        var createIndex = AskInt("Position for new text?", 0, embedCount);
                        config.Templates[templateName].Embeds ??= [];
                        config.Templates[templateName].Embeds!.Insert(createIndex, string.Empty);
                        break;

                    case "X":
                        return;
                }
            }
        }
        #endregion

        #region Utils
        private static string AskString(string question, bool bypassEmptyCheck = false) //todo: how are newlines handled during save?
        {
            while (true)
            {
                Console.Write(question + "\n> ");
                var res = Console.ReadLine();
                if (!string.IsNullOrWhiteSpace(res) || bypassEmptyCheck)
                {
                    return res!;
                }
            }
        }

        private static int AskInt(string question, int lowest = int.MinValue, int highest = int.MaxValue)
        {
            while (true)
            {
                var res = AskString($"{question} (Min: {lowest}, Max: {highest})");
                if (int.TryParse(res, out var val) && val >= lowest && val <= highest)
                {
                    return val;
                }
            }
        }

        private static string CreateSelectMenu(params (string, string)[] options)
        {
            Console.WriteLine($"{string.Join("\n", options.Select(x => $" {x.Item1} > {x.Item2}"))}\n");
            return AskString("What would you like to do?").ToUpper();
        }

        private static string CreateFullMenu(string menuText, params (string, string)[] options)
        {
            Console.Clear();
            Console.WriteLine($"{menuText}\n");
            return CreateSelectMenu(options);
        }

        private static JsonException? ValidateJson(string json)
        {
            try
            {
                using (JsonDocument.Parse(json))
                {
                    return null;
                }
            }
            catch (JsonException ex)
            {
                return ex;
            }
        }
        #endregion
    }
}
