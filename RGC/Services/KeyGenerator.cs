using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace RGC.Services
{
    public static class KeyGenerator
    {
        private static readonly HttpClient _http = new();

        public static async Task<string> GenerateKeysAsync(int count, string prefix = "RGC")
        {
            var json = LoadServiceAccountJson();
            using var doc = JsonDocument.Parse(json);
            var projectId = doc.RootElement.GetProperty("project_id").GetString()!;

            var token = await GetTokenAsync(json);

            var created = new List<string>();
            for (var i = 0; i < count; i++)
            {
                var key = $"{prefix}-{RandomNumberGenerator.GetInt32(100000, 999999)}";
                var docId = Guid.NewGuid().ToString("N");

                var url = $"https://firestore.googleapis.com/v1/projects/{projectId}/databases/(default)/documents/activation_keys?documentId={docId}";
                var body = JsonSerializer.Serialize(new
                {
                    fields = new
                    {
                        key = new { stringValue = key },
                        used = new { booleanValue = false },
                        created_at = new { stringValue = DateTime.UtcNow.ToString("yyyy-MM-dd") },
                        machine_id = new { stringValue = "" },
                        activated_at = new { stringValue = "" }
                    }
                });

                var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                request.Content = new StringContent(body, Encoding.UTF8, "application/json");

                var resp = await _http.SendAsync(request);
                if (resp.IsSuccessStatusCode)
                    created.Add(key);
            }

            return $"Создано {created.Count} из {count} ключей:\n{string.Join("\n", created)}";
        }

        private static string LoadServiceAccountJson()
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                foreach (var name in assembly.GetManifestResourceNames())
                    if (name.Contains("firebase-key"))
                        using (var s = assembly.GetManifestResourceStream(name))
                        using (var r = new StreamReader(s!))
                            return r.ReadToEnd();
            }
            catch { }

            var paths = new[]
            {
                Path.Combine(AppContext.BaseDirectory, "firebase-key.json"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "firebase-key.json"),
                "firebase-key.json"
            };
            foreach (var p in paths)
                if (File.Exists(p)) return File.ReadAllText(p);

            throw new FileNotFoundException("firebase-key.json не найден");
        }

        private static async Task<string> GetTokenAsync(string json)
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var clientEmail = root.GetProperty("client_email").GetString()!;
            var privateKey = root.GetProperty("private_key").GetString()!;

            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var header = Base64UrlEncode(JsonSerializer.Serialize(new { alg = "RS256", typ = "JWT" }));
            var claims = Base64UrlEncode(JsonSerializer.Serialize(new
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

            var assertion = $"{header}.{claims}.{Base64UrlEncode(sig)}";

            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("grant_type", "urn:ietf:params:oauth:grant-type:jwt-bearer"),
                new KeyValuePair<string, string>("assertion", assertion)
            });

            var resp = await _http.PostAsync("https://oauth2.googleapis.com/token", content);
            var respBody = await resp.Content.ReadAsStringAsync();
            using var tokenDoc = JsonDocument.Parse(respBody);
            return tokenDoc.RootElement.GetProperty("access_token").GetString()!;
        }

        private static string Base64UrlEncode(string s) => Base64UrlEncode(Encoding.UTF8.GetBytes(s));
        private static string Base64UrlEncode(byte[] data) =>
            Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
