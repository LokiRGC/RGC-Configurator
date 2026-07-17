using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using RGC.Models;

namespace RGC.Services
{
    public static class FirebaseService
    {
        private static readonly HttpClient _http = new();
        private static string? _token;
        private static string? _projectId;

        private static async Task<string> GetTokenAsync()
        {
            if (!string.IsNullOrEmpty(_token)) return _token;

            var json = LoadKey();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            _projectId = root.GetProperty("project_id").GetString();
            var clientEmail = root.GetProperty("client_email").GetString()!;
            var privateKey = root.GetProperty("private_key").GetString()!;

            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var header = B64(JsonSerializer.Serialize(new { alg = "RS256", typ = "JWT" }));
            var claims = B64(JsonSerializer.Serialize(new
            {
                iss = clientEmail,
                scope = "https://www.googleapis.com/auth/datastore",
                aud = "https://oauth2.googleapis.com/token",
                exp = now + 3600,
                iat = now
            }));

            using var rsa = RSA.Create();
            rsa.ImportPkcs8PrivateKey(Convert.FromBase64String(
                privateKey.Replace("-----BEGIN PRIVATE KEY-----", "")
                    .Replace("-----END PRIVATE KEY-----", "")
                    .Replace("\n", "").Replace("\r", "")), out _);
            var sig = rsa.SignData(Encoding.UTF8.GetBytes($"{header}.{claims}"),
                HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

            var resp = await _http.PostAsync("https://oauth2.googleapis.com/token",
                new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("grant_type", "urn:ietf:params:oauth:grant-type:jwt-bearer"),
                    new KeyValuePair<string, string>("assertion", $"{header}.{claims}.{B64(sig)}")
                }));

            var body = await resp.Content.ReadAsStringAsync();
            _token = JsonDocument.Parse(body).RootElement.GetProperty("access_token").GetString()!;
            return _token;
        }

        public static async Task<(string? version, string? downloadUrl, string? notes)> CheckUpdateAsync()
        {
            try
            {
                var token = await GetTokenAsync();
                var url = $"https://firestore.googleapis.com/v1/projects/{_projectId}/databases/(default)/documents/updates/latest";

                var req = new HttpRequestMessage(HttpMethod.Get, url);
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                var resp = await _http.SendAsync(req);
                if (!resp.IsSuccessStatusCode) return (null, null, null);

                var body = await resp.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(body);
                var fields = doc.RootElement.GetProperty("fields");

                var version = fields.GetProperty("version").GetProperty("stringValue").GetString();
                var downloadUrl = fields.GetProperty("download_url").GetProperty("stringValue").GetString();
                var notes = fields.TryGetProperty("release_notes", out var n)
                    ? n.GetProperty("stringValue").GetString() : null;

                return (version, downloadUrl, notes);
            }
            catch { return (null, null, null); }
        }

        public static async Task<bool> SetUpdateVersionAsync(string version, string downloadUrl = "", string notes = "")
        {
            try
            {
                var token = await GetTokenAsync();
                var url = $"https://firestore.googleapis.com/v1/projects/{_projectId}/databases/(default)/documents/updates/latest";

                var fields = new Dictionary<string, object>
                {
                    ["version"] = new { stringValue = version },
                    ["download_url"] = new { stringValue = downloadUrl }
                };
                if (!string.IsNullOrEmpty(notes))
                    fields["release_notes"] = new { stringValue = notes };

                var body = JsonSerializer.Serialize(new { fields });

                var req = new HttpRequestMessage(HttpMethod.Patch, url);
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                req.Content = new StringContent(body, Encoding.UTF8, "application/json");
                var resp = await _http.SendAsync(req);
                return resp.IsSuccessStatusCode;
            }
            catch { return false; }
        }

        public static async Task<List<Announcement>> GetAnnouncementsAsync(string currentVersion = "")
        {
            var result = new List<Announcement>();
            try
            {
                var token = await GetTokenAsync();
                var url = $"https://firestore.googleapis.com/v1/projects/{_projectId}/databases/(default)/documents/announcements";

                var req = new HttpRequestMessage(HttpMethod.Get, url);
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                var resp = await _http.SendAsync(req);
                if (!resp.IsSuccessStatusCode) return result;

                var body = await resp.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(body);
                if (!doc.RootElement.TryGetProperty("documents", out var docs)) return result;

                var now = DateTime.UtcNow;
                foreach (var d in docs.EnumerateArray())
                {
                    var fields = d.GetProperty("fields");

                    var text = fields.GetProperty("text").GetProperty("stringValue").GetString() ?? "";
                    if (string.IsNullOrEmpty(text)) continue;

                    // Expiry check
                    if (fields.TryGetProperty("expires_at", out var exp))
                    {
                        var expStr = exp.GetProperty("timestampValue").GetString();
                        if (DateTime.TryParse(expStr, out var expDate) && expDate < now)
                            continue;
                    }

                    // Version range check
                    if (!string.IsNullOrEmpty(currentVersion))
                    {
                        if (fields.TryGetProperty("min_version", out var minV))
                        {
                            var minStr = minV.GetProperty("stringValue").GetString();
                            if (!string.IsNullOrEmpty(minStr) && CompareVersions(currentVersion, minStr) < 0)
                                continue;
                        }
                        if (fields.TryGetProperty("max_version", out var maxV))
                        {
                            var maxStr = maxV.GetProperty("stringValue").GetString();
                            if (!string.IsNullOrEmpty(maxStr) && CompareVersions(currentVersion, maxStr) > 0)
                                continue;
                        }
                    }

                    var type = fields.TryGetProperty("type", out var t)
                        ? t.GetProperty("stringValue").GetString() ?? "info" : "info";

                    var textEn = fields.TryGetProperty("text_en", out var en)
                        ? en.GetProperty("stringValue").GetString() ?? "" : "";

                    result.Add(new Announcement { Text = text, TextEn = textEn, Type = type });
                }
            }
            catch { }
            return result;
        }

        private static int CompareVersions(string a, string b)
        {
            a = a.TrimStart('v', 'V');
            b = b.TrimStart('v', 'V');
            var partsA = a.Split('.').Select(s => int.TryParse(s, out var n) ? n : 0).ToArray();
            var partsB = b.Split('.').Select(s => int.TryParse(s, out var n) ? n : 0).ToArray();
            for (int i = 0; i < Math.Max(partsA.Length, partsB.Length); i++)
            {
                var va = i < partsA.Length ? partsA[i] : 0;
                var vb = i < partsB.Length ? partsB[i] : 0;
                if (va != vb) return va.CompareTo(vb);
            }
            return 0;
        }

        private static string LoadKey()
        {
            try
            {
                var asm = Assembly.GetExecutingAssembly();
                foreach (var n in asm.GetManifestResourceNames())
                    if (n.Contains("firebase-key"))
                        using (var s = asm.GetManifestResourceStream(n)!)
                        using (var r = new StreamReader(s)) return r.ReadToEnd();
            }
            catch { }

            var paths = new[] {
                Path.Combine(AppContext.BaseDirectory, "firebase-key.json"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "firebase-key.json"),
                "firebase-key.json"
            };
            foreach (var p in paths)
                if (File.Exists(p)) return File.ReadAllText(p);

            throw new FileNotFoundException("firebase-key.json");
        }

        private static string B64(string s) => B64(Encoding.UTF8.GetBytes(s));
        private static string B64(byte[] d) =>
            Convert.ToBase64String(d).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
