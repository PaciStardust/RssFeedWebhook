using RssFeedWebhook.Json;
using System.ServiceModel.Syndication;
using System.Text;
using System.Text.RegularExpressions;

namespace RssFeedWebhook
{
    internal class TemplateApplicator
    {
        private static readonly Regex _specialTokenMatcher = new(@"\[\$(?'Token'[A-Za-z]+)(?:\[(?::(?'Trim'[1-9][0-9]*)|\?'(?'Null'[^']*)'|<'(?'Pre'[^']*)'|>'(?'Post'[^']*)')\])*\$\]");
        private static readonly Regex _newlineFilter = new(@"\r\n?|\n");

        private readonly Token[]? _tokensContent;
        private readonly Token[][]? _tokensEmbeds;
        private readonly string _emptyText;

        private static readonly Dictionary<string, (string, Func<SyndicationItem, string>)> _tokenFunctions = new()
        {
            { "FeedAuthorsByEmail",         ("All feed authors by email only",                  x => string.Join(", ", x.SourceFeed.Authors?.Select(y => ParseSyndicationPerson(y, PersonParsingMode.Email)) ?? [])) },
            { "FeedAuthorsByName",          ("All feed authors by name only",                   x => string.Join(", ", x.SourceFeed.Authors?.Select(y => ParseSyndicationPerson(y, PersonParsingMode.Name)) ?? [])) },
            { "FeedAuthorsSmart",           ("All feed authors by name and email",              x => string.Join(", ", x.SourceFeed.Authors?.Select(y => ParseSyndicationPerson(y, PersonParsingMode.Smart)) ?? [])) },
            { "FeedCategories",             ("All feed categories",                             x => string.Join(", ", x.SourceFeed.Categories?.Select(y => y.Name) ?? [])) },
            { "FeedContributorsByEmail",    ("All feed contributors by email only",             x => string.Join(", ", x.SourceFeed.Contributors?.Select(y => ParseSyndicationPerson(y, PersonParsingMode.Email)) ?? [])) },
            { "FeedContributorsByName",     ("All feed contributors by name only",              x => string.Join(", ", x.SourceFeed.Contributors?.Select(y => ParseSyndicationPerson(y, PersonParsingMode.Name)) ?? [])) },
            { "FeedContributorsSmart",      ("All feed contributors by name and email",         x => string.Join(", ", x.SourceFeed.Contributors?.Select(y => ParseSyndicationPerson(y, PersonParsingMode.Smart)) ?? [])) },
            { "FeedCopyright",              ("Feed copyright text",                             x => x.SourceFeed.Copyright?.Text ?? string.Empty) },
            { "FeedDescription",            ("Desciptuion of feed",                             x => x.SourceFeed.Description?.Text ?? string.Empty) },
            { "FeedId",                     ("Identifier of feed",                              x => x.SourceFeed.Id ?? string.Empty) },
            { "FeedImage",                  ("Image URL for feed",                              x => x.SourceFeed.ImageUrl?.AbsolutePath ?? string.Empty) },
            { "FeedLanguage",               ("Language of feed",                                x => x.SourceFeed.Language ?? string.Empty) },
            { "FeedLastUpdated",            ("Time and date of last feed update",               x => x.SourceFeed.LastUpdatedTime.ToString()) },
            { "FeedLinks",                  ("List of links from feed",                         x => string.Join(", ", x.SourceFeed.Links?.Select(y => y.Uri.AbsolutePath) ?? [])) },
            { "FeedTitle",                  ("Title of feed",                                   x => x.SourceFeed.Title?.Text ?? string.Empty) },
            { "FeedUrl",                    ("Url of feed",                                     x => x.SourceFeed.BaseUri?.AbsoluteUri ?? string.Empty) },

            { "ItemAuthorsByEmail",         ("All item authors by email only",                  x => string.Join(", ", x.Authors?.Select(y => ParseSyndicationPerson(y, PersonParsingMode.Email)) ?? [])) },
            { "ItemAuthorsByName",          ("All item authors by name only",                   x => string.Join(", ", x.Authors?.Select(y => ParseSyndicationPerson(y, PersonParsingMode.Name)) ?? [])) },
            { "ItemAuthorsSmart",           ("All item contributors by name and email",         x => string.Join(", ", x.Authors?.Select(y => ParseSyndicationPerson(y, PersonParsingMode.Smart)) ?? [])) },
            { "ItemCategories",             ("List of item categories",                         x => string.Join(", ", x.Categories)) },
            { "ItemContentType",            ("Content type of item",                            x => x.Content?.Type ?? string.Empty) },
            { "ItemContent",                ("Content of item",                                 x => ParseContent(x.Content) ?? string.Empty)},
            { "ItemContentSmart",           ("Content or summary of item",                      x => ParseContent(x.Content) ?? (x.Summary is null ? string.Empty : ParseTextContent(x.Summary))) },
            { "ItemContributorsByEmail",    ("All item contributors by email only",             x => string.Join(", ", x.Contributors?.Select(y => ParseSyndicationPerson(y, PersonParsingMode.Email)) ?? [])) },
            { "ItemContributorsByName",     ("All item contributors by name only",              x => string.Join(", ", x.Contributors?.Select(y => ParseSyndicationPerson(y, PersonParsingMode.Name)) ?? [])) },
            { "ItemContributorsSmart",      ("All item contributors by name and email",         x => string.Join(", ", x.Contributors?.Select(y => ParseSyndicationPerson(y, PersonParsingMode.Smart)) ?? [])) },
            { "ItemCopyright",              ("Item copyright text",                             x => x.Copyright?.Text ?? string.Empty) },
            { "ItemId",                     ("Identifier of item",                              x => x.Id ?? string.Empty) },
            { "ItemLastUpdated",            ("Time and date of last item update",               x => x.LastUpdatedTime.ToString()) },
            { "ItemLinks",                  ("List of links from item",                         x => string.Join(", ", x.Links?.Select(y => y.Uri.AbsolutePath) ?? [])) },
            { "ItemPublished",              ("Time and date of publishing time",                x => x.PublishDate.ToString()) },
            { "ItemSummary",                ("Summary of item contents",                        x => x.Summary is null ? string.Empty : ParseTextContent(x.Summary)) },
            { "ItemTitle",                  ("Title of item",                                   x => x.Title?.Text ?? string.Empty) },
            { "ItemTimeSmart",              ("HH:MM and date of last item update or publish",   x => (x.LastUpdatedTime > DateTimeOffset.MinValue ? x.LastUpdatedTime : x.PublishDate).ToString("yyyy/MM/dd HH:mm")) },
            { "ItemTimeSmartFull",          ("HH:MM:SS and date of last item update or publish",x => (x.LastUpdatedTime > DateTimeOffset.MinValue ? x.LastUpdatedTime : x.PublishDate).ToString("yyyy/MM/dd HH:mm:ss")) },
            { "ItemTimeSmartDate",          ("Date of last item update or publish",             x => (x.LastUpdatedTime > DateTimeOffset.MinValue ? x.LastUpdatedTime : x.PublishDate).ToString("yyyy/MM/dd")) },
            { "ItemTimeSmartUnix",          ("Unix time of last item update or publish",        x => (x.LastUpdatedTime > DateTimeOffset.MinValue ? x.LastUpdatedTime : x.PublishDate).ToUnixTimeSeconds().ToString()) },
            { "ItemUrl",                    ("Url of item",                                     x => x.BaseUri?.AbsoluteUri ?? string.Empty) }
        };

        public static readonly string TokenDescription = new Func<string>(() =>
        {
            var longestKey = _tokenFunctions.Keys.Max(x => x.Length);
            var tokenText = string.Join("\n", _tokenFunctions.Select(x => $" - {x.Key.PadRight(longestKey)} => {x.Value.Item1}"));
            return $"{tokenText}\n\nUsage example:\nRaw Text \"New post from [$ItemAuthors$] with title '[$ItemTitle$]'\"\nBecomes  \"New post from Paci with title 'This is how placeholder tokens work!'\"";
        }).Invoke(); //This is jank lol

        public static readonly string TokenList = string.Join(", ", _tokenFunctions.Keys);

        private static readonly (string, string, string)[] _tokenModifierDescriptions =
        [
            ("[:x]", "[:99]", "Trims text to x characters"),
            ("[?'x']", "[?'example']", "Replaces text with x if missing"),
            ("[<'x']", "[<'example']", "Adds x to front if text not missing"),
            ("[>'x']", "[>'example']", "Adds x to end if text not missing"),
        ];

        public static readonly string TokenModifierDescription = new Func<string>(() =>
        {
            var longestToken = _tokenModifierDescriptions.Max(x => x.Item1.Length);
            var longestDesc = _tokenModifierDescriptions.Max(x => x.Item3.Length);
            var tokenModifierText = string.Join("\n", _tokenModifierDescriptions.Select(x => $" - {x.Item1.PadRight(longestToken)} => {x.Item3.PadRight(longestDesc)} (Example: {x.Item2})"));
            return $"{tokenModifierText}\n\nUsage example:\nRaw Text \"[$ItemAuthors[?'No authors found'][<'Authors: ']$]!\"\nBecomes  \"No authors found!\" or \"Authors: Paci!\"";
        }).Invoke(); //More jank

        public static readonly string TokenModifierList = string.Join(", ", _tokenModifierDescriptions.Select(x => x.Item1));
        

        public TemplateApplicator(Template template)
        {
            _tokensContent = template.Content is null ? null : GenerateTokens(template.Content);

            if (template.Embeds is not null && template.Embeds.Count != 0)
            {
                _tokensEmbeds = new Token[template.Embeds.Count][];

                for (var i = 0; i < template.Embeds.Count; i++)
                {
                    _tokensEmbeds[i] = GenerateTokens(template.Embeds[i]);
                }
            }

            _emptyText = template.EmptyText ?? "[null]";
        }

        #region Token Parsing
        private static Token[] GenerateTokens(string input)
        {
            var matches = _specialTokenMatcher.Matches(input);
            if (matches is null || matches.Count == 0)
            {
                return [new(input)];
            }

            List<Token> tokensParsed = [];
            int nextIndex = 0;
            foreach (Match match in matches)
            {
                if (match.Index != nextIndex)
                {
                    tokensParsed.Add(new(input[nextIndex..match.Index]));
                }

                var tokenName = match.Groups["Token"].Value;
                var tokenDict = new Dictionary<string, string>();
                
                foreach (Group group in match.Groups)
                {
                    if (group.Success && group.Name != "0" && group.Name != "Token")
                    {
                        tokenDict[group.Name] = group.Value;
                    }
                }

                tokensParsed.Add(new(tokenName, tokenDict));
                nextIndex = match.Index + match.Length; 
            }

            if (nextIndex != input.Length)
            {
                tokensParsed.Add(new(input[nextIndex..]));
            }
            return tokensParsed.ToArray();
        }

        public string Apply(SyndicationItem item)
        {
            var parameters = new Dictionary<string, string>();

            if (_tokensContent is not null)
            {
                parameters["content"] = $"\"{ApplyTokens(item, _tokensContent)}\"";
            }

            if (_tokensEmbeds is not null && _tokensEmbeds.Length != 0)
            {
                var embeds = new List<string>();

                foreach (var embed in _tokensEmbeds)
                {
                    embeds.Add(ApplyTokens(item, embed));
                }

                parameters.Add("embeds", $"[{string.Join(", ", embeds)}]");
            }

            var formattedParameters = parameters.Select(x => $"\"{x.Key}\": {x.Value}");
            return $"{{{string.Join(", ", formattedParameters)}}}";
        }

        private string ApplyTokens(SyndicationItem item, Token[] tokens)
        {
            var sb = new StringBuilder();
            foreach (var token in tokens)
            {
                if (token.SpecialTokenInfo is null)
                {
                    sb.Append(token.Content);
                    continue;
                }
                     
                if (!_tokenFunctions.TryGetValue(token.Content, out var tokenFunc))
                {
                    Console.WriteLine($"WARNING: Could not find special token \"{token.Content}\" to apply for item {item.Id}");
                    sb.Append($"[$UnknownToken{token.Content}$]");
                    continue;
                }

                var result = tokenFunc.Item2(item);
                if (string.IsNullOrWhiteSpace(result))
                {
                    var nullText = token.SpecialTokenInfo.TryGetValue("Null", out string? valueNull)
                        ? valueNull
                        : _emptyText;
                    sb.Append(nullText);
                    continue;
                }


                if (token.SpecialTokenInfo.TryGetValue("Trim", out string? valueTrim))
                {
                    if (int.TryParse(valueTrim, out int valueTrimParsed))
                    {
                        if (result.Length > valueTrimParsed)
                        {
                            result = valueTrimParsed < 10
                            ? result[..valueTrimParsed]
                            : result[..(valueTrimParsed - 3)] + "...";
                        }
                    }
                    else
                    {
                        Console.WriteLine($"WARNING: Could not convert value for special token modifier trim to apply for item {item.Id}");
                    }
                }
                result = _newlineFilter.Replace(result, "\\n").Replace("\"", "\\\"");

                if (token.SpecialTokenInfo.TryGetValue("Pre", out var valuePre))
                {
                    result = valuePre + result;
                }
                if (token.SpecialTokenInfo.TryGetValue("Post", out var valuePost))
                {
                    result += valuePost;
                }

                sb.Append(result);
            }
            return sb.ToString();
        }

        public bool IsEmpty()
        {
            return _tokensContent is null && (_tokensEmbeds?.Length ?? 0) == 0;
        }
        #endregion

        #region Content Parsing
        private static string? ParseContent(SyndicationContent content)
        {
            if (content is null)
            {
                return null;
            }
            if (content is TextSyndicationContent textContent)
            {
                return ParseTextContent(textContent);
            }
            if (content is XmlSyndicationContent xmlContent)
            {
                return xmlContent.ReadContent<string>();
            }
            if (content is UrlSyndicationContent urlContent)
            {
                return urlContent.Url.AbsolutePath;
            }
            return null;
        }

        private static string ParseTextContent(TextSyndicationContent textContent)
        {
            return textContent.Type switch
            {
                "html" => HtmlUtils.ConvertToPlainText(textContent.Text),
                _ => textContent.Text
            };
        }
        #endregion

        #region Other Parsing
        private enum PersonParsingMode
        {
            Name,
            Email,
            Smart
        }

        private static string ParseSyndicationPerson(SyndicationPerson person, PersonParsingMode mode)
        {
            switch (mode)
            {
                case PersonParsingMode.Name:
                    if (!string.IsNullOrWhiteSpace(person.Name))
                    {
                        return person.Name;
                    }
                    break;
                case PersonParsingMode.Email:
                    if (!string.IsNullOrWhiteSpace(person.Email))
                    {
                        return person.Email;
                    }
                    break;
                case PersonParsingMode.Smart:
                    if (!string.IsNullOrWhiteSpace(person.Name))
                    {
                        return $"{person.Name}{(string.IsNullOrWhiteSpace(person.Email) ? string.Empty : $"({person.Email})")}";
                    }
                    if (!string.IsNullOrWhiteSpace(person.Email))
                    {
                        return person.Email;
                    }
                    break;
                default:
                    break;
            }
            return "Unknown";
        }
        #endregion
    }

    internal record Token(string Content, Dictionary<string, string>? SpecialTokenInfo = null);
}