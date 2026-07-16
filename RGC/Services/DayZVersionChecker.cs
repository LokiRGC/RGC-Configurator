using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace RGC.Services
{
    public static class DayZVersionChecker
    {
        private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(8) };

        public static string? GetInstalledVersion(string serverPath)
        {
            try
            {
                var exe = System.IO.Path.Combine(serverPath, "DayZServer_x64.exe");
                if (!System.IO.File.Exists(exe)) return null;
                var vi = System.Diagnostics.FileVersionInfo.GetVersionInfo(exe);
                var v = vi.ProductVersion?.Split(' ')[0] ?? vi.FileVersion ?? "";
                return v.Length > 10 ? v[..10] : v;
            }
            catch { return null; }
        }

        public static async Task<string?> GetLatestVersionAsync()
        {
            try
            {
                var json = await _http.GetStringAsync("https://store.steampowered.com/api/appdetails?appids=223350");
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("223350", out var app) &&
                    app.TryGetProperty("data", out var data) &&
                    data.TryGetProperty("builds", out var builds) &&
                    builds.GetArrayLength() > 0)
                {
                    var latest = builds[0];
                    if (latest.TryGetProperty("buildid", out var bid))
                        return bid.GetString();
                }
            }
            catch { }
            return null;
        }

        public static async Task<(string? installed, string? latest, bool? isLatest)> CheckVersionAsync(string serverPath)
        {
            var installed = GetInstalledVersion(serverPath);
            var latest = await GetLatestVersionAsync();
            if (installed != null && latest != null)
                return (installed, latest, installed.Contains(latest) || latest.Contains(installed));
            return (installed, latest, null);
        }
    }
}
