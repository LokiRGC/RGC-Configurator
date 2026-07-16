using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Media;

namespace RGC.Services
{
    public class ModUpdateChecker
    {
        private static readonly HttpClient _http = CreateClient();

        private static HttpClient CreateClient()
        {
            var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            client.Timeout = TimeSpan.FromSeconds(15);
            return client;
        }

        public List<ModStatus> CheckMods(string serverDir, string[] modNames)
        {
            var results = new List<ModStatus>();

            foreach (var modName in modNames)
            {
                var modDir = Path.Combine(serverDir, modName);
                var metaFile = Path.Combine(modDir, "meta.cpp");

                if (!Directory.Exists(modDir) || !File.Exists(metaFile))
                {
                    results.Add(new ModStatus(modName, "❌", "meta.cpp не найден", Colors.Red));
                    continue;
                }

                try
                {
                    var metaContent = File.ReadAllText(metaFile);
                    var pubId = ExtractPublishedId(metaContent);

                    if (pubId == null)
                    {
                        results.Add(new ModStatus(modName, "❓", "PublishedID не найден", Colors.Orange));
                        continue;
                    }

                    var workshopDir = Path.Combine(serverDir, "steamapps", "workshop", "content", "221100", pubId);
                    var workshopMeta = Path.Combine(workshopDir, "meta.cpp");

                    string status;
                    System.Windows.Media.Color color;

                    if (!Directory.Exists(workshopDir) || !File.Exists(workshopMeta))
                    {
                        status = "Не скачан в workshop";
                        color = Colors.DodgerBlue;
                    }
                    else
                    {
                        var modTime = File.GetLastWriteTimeUtc(metaFile);
                        var wsTime = File.GetLastWriteTimeUtc(workshopMeta);

                        if (wsTime > modTime)
                        {
                            status = "Доступно обновление";
                            color = Colors.Orange;
                        }
                        else
                        {
                            status = "Актуален";
                            color = Colors.LimeGreen;
                        }
                    }

                    results.Add(new ModStatus(modName, "✅", status, color, pubId, null));
                }
                catch (Exception ex)
                {
                    results.Add(new ModStatus(modName, "❌", ex.Message, Colors.Red));
                }
            }

            return results;
        }

        public static async Task<string?> CheckRepackAllowed(string pubId)
        {
            try
            {
                var desc = await FetchDescription(pubId);
                if (desc == null)
                    return "⚠️ API недоступен";
                if (string.IsNullOrWhiteSpace(desc))
                    return "❓ описание пустое";

                var allowPatterns = new[]
                {
                    @"разреша(?:ю|ется|ено)\s*(?:переупаковк|использ|сборк)",
                    @"repack(?:ing)?\s*(?:is\s*)?allowed",
                    @"repackage\s*(?:is\s*)?allowed",
                    @"feel\s*free\s*to\s*(?:repack|repackage|include)",
                    @"можно\s*(?:переупаковывать|использовать)",
                    @"allowed\s*to\s*(?:repack|repackage|mod.?pack)",
                };

                var denyPatterns = new[]
                {
                    @"запреща(?:ю|ется|ено)\s*(?:переупаковк|использ|сборк)",
                    @"(?:you\s+are\s+)?not\s+allowed\s+to\s+(?:repack|repackage)",
                    @"repack(?:ing)?\s*(?:is\s*)?(?:not\s*)?(?:disallowed|prohibited|forbidden)",
                    @"do\s*not\s*(?:repack|repackage)",
                    @"не\s*(?:переупаковывать|использовать|разрешено)",
                    @"no\s*repack(?:ing)?",
                };

                foreach (var pat in allowPatterns)
                    if (Regex.IsMatch(desc, pat, RegexOptions.IgnoreCase))
                        return "✅ Можно";

                foreach (var pat in denyPatterns)
                    if (Regex.IsMatch(desc, pat, RegexOptions.IgnoreCase))
                        return "❌ Нельзя";

                return "❓ нет ключевых слов";
            }
            catch (Exception ex)
            {
                return $"⚠️ {ex.GetType().Name}";
            }
        }

        private static async Task<string?> FetchDescription(string pubId)
        {
            // Try Steam Web API first
            try
            {
                var form = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("itemcount", "1"),
                    new KeyValuePair<string, string>("publishedfileids[0]", pubId),
                });

                var response = await _http.PostAsync(
                    "https://api.steampowered.com/ISteamRemoteStorage/GetPublishedFileDetails/v1/",
                    form);

                var json = await response.Content.ReadAsStringAsync();
                if (json.Contains("\"result\":1"))
                {
                    var match = Regex.Match(json,
                        @"""description""\s*:\s*""((?:[^""\\]|\\.)*)""",
                        RegexOptions.Singleline);

                    if (match.Success)
                    {
                        var d = Regex.Replace(match.Groups[1].Value, @"\\[rn""\\]", " ").ToLowerInvariant();
                        if (!string.IsNullOrWhiteSpace(d)) return d;
                    }
                }
                return null;
            }
            catch { }

            // Fallback: scrape HTML with User-Agent
            try
            {
                var url = $"https://steamcommunity.com/sharedfiles/filedetails/?id={pubId}&l=english";
                var html = await _http.GetStringAsync(url);

                var patterns = new[]
                {
                    @"class=""workshopItemDescription[^""]*""[^>]*>(.*?)</div>",
                    @"""description"">(.*?)</div>",
                    @"class=""description[^""]*"">(.*?)</div>",
                };

                foreach (var pat in patterns)
                {
                    var match = Regex.Match(html, pat, RegexOptions.Singleline | RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        var d = Regex.Replace(match.Groups[1].Value, @"<[^>]+>", "").ToLowerInvariant();
                        if (!string.IsNullOrWhiteSpace(d))
                            return d;
                    }
                }
            }
            catch { }

            return null;
        }

        private static string? ExtractPublishedId(string content)
        {
            var match = Regex.Match(content, @"PublishedID\s*=\s*(\d+)", RegexOptions.IgnoreCase);
            return match.Success ? match.Groups[1].Value : null;
        }
    }

    public class ModStatus
    {
        public string ModName { get; }
        public string Icon { get; }
        public string Status { get; }
        public SolidColorBrush Brush { get; }
        public string? PublishedId { get; }
        public string? RepackAllowed { get; }

        public ModStatus(string modName, string icon, string status, System.Windows.Media.Color color,
            string? pubId = null, string? repack = null)
        {
            ModName = modName;
            Icon = icon;
            Status = status;
            Brush = new SolidColorBrush(color);
            Brush.Freeze();
            PublishedId = pubId;
            RepackAllowed = repack;
        }
    }
}
