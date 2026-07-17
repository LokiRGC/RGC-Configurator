using System;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace RGC.Services
{
    public static class ActivationService
    {
        private static readonly HttpClient _http = new();
        private static bool _initialized;
        private static string? _accessToken;

        private static readonly string ActFile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RGC", ".activated");

        public static bool IsActivated
        {
            get
            {
                try { return File.Exists(ActFile) && File.ReadAllText(ActFile).Trim().Length > 0; }
                catch { return false; }
            }
        }

        private static string MachineId =>
            $"{Environment.MachineName}-{Environment.UserName}";

        private static string LoadServiceAccountJson()
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                foreach (var name in assembly.GetManifestResourceNames())
                {
                    if (name.Contains("firebase-key"))
                    {
                        using var s = assembly.GetManifestResourceStream(name);
                        if (s != null)
                        {
                            using var r = new StreamReader(s);
                            return r.ReadToEnd();
                        }
                    }
                }
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

        private static async Task<string> GetAccessTokenAsync()
        {
            if (!string.IsNullOrEmpty(_accessToken)) return _accessToken;

            var json = LoadServiceAccountJson();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var clientEmail = root.GetProperty("client_email").GetString()!;
            var privateKey = root.GetProperty("private_key").GetString()!;
            var projectId = root.GetProperty("project_id").GetString()!;

            // Create JWT assertion
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
            var signature = SignJwt($"{header}.{claims}", privateKey);
            var assertion = $"{header}.{claims}.{signature}";

            // Exchange for access token
            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("grant_type", "urn:ietf:params:oauth:grant-type:jwt-bearer"),
                new KeyValuePair<string, string>("assertion", assertion)
            });

            var resp = await _http.PostAsync("https://oauth2.googleapis.com/token", content);
            var respBody = await resp.Content.ReadAsStringAsync();
            using var tokenDoc = JsonDocument.Parse(respBody);

            if (!resp.IsSuccessStatusCode)
            {
                var err = tokenDoc.RootElement.TryGetProperty("error_description", out var ed)
                    ? ed.GetString() : respBody;
                throw new Exception($"Ошибка получения токена: {err}");
            }

            _accessToken = tokenDoc.RootElement.GetProperty("access_token").GetString();
            return _accessToken!;
        }

        private static string SignJwt(string data, string privateKeyPem)
        {
            using var rsa = System.Security.Cryptography.RSA.Create();
            var keyBytes = ConvertPemToBytes(privateKeyPem);
            rsa.ImportPkcs8PrivateKey(keyBytes, out _);
            var sig = rsa.SignData(Encoding.UTF8.GetBytes(data), System.Security.Cryptography.HashAlgorithmName.SHA256, System.Security.Cryptography.RSASignaturePadding.Pkcs1);
            return Base64UrlEncode(sig);
        }

        private static byte[] ConvertPemToBytes(string pem)
        {
            var base64 = pem
                .Replace("-----BEGIN PRIVATE KEY-----", "")
                .Replace("-----END PRIVATE KEY-----", "")
                .Replace("\n", "").Replace("\r", "");
            return Convert.FromBase64String(base64);
        }

        private static string Base64UrlEncode(string s) =>
            Base64UrlEncode(Encoding.UTF8.GetBytes(s));

        private static string Base64UrlEncode(byte[] data) =>
            Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        public static async Task<string?> ActivateAsync(string key)
        {
            try
            {
                var token = await GetAccessTokenAsync();
                var json = LoadServiceAccountJson();
                var projectId = JsonDocument.Parse(json).RootElement.GetProperty("project_id").GetString();

                // Query Firestore REST API
                var queryUrl = $"https://firestore.googleapis.com/v1/projects/{projectId}/databases/(default)/documents:runQuery";
                var queryBody = JsonSerializer.Serialize(new
                {
                    structuredQuery = new
                    {
                        from = new[] { new { collectionId = "activation_keys" } },
                        where = new
                        {
                            fieldFilter = new
                            {
                                field = new { fieldPath = "key" },
                                op = "EQUAL",
                                value = new { stringValue = key.Trim() }
                            }
                        },
                        limit = 1
                    }
                });

                var request = new HttpRequestMessage(HttpMethod.Post, queryUrl);
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                request.Content = new StringContent(queryBody, Encoding.UTF8, "application/json");

                var resp = await _http.SendAsync(request);
                var respBody = await resp.Content.ReadAsStringAsync();

                if (!resp.IsSuccessStatusCode)
                    return $"Ошибка Firestore ({resp.StatusCode}): {respBody}";

                using var resultDoc = JsonDocument.Parse(respBody);
                var arr = resultDoc.RootElement;

                if (arr.GetArrayLength() == 0 || !arr[0].TryGetProperty("document", out var doc))
                    return "Ключ не найден";

                // Check if used
                var fields = doc.GetProperty("fields");
                if (fields.TryGetProperty("used", out var usedField) &&
                    usedField.TryGetProperty("booleanValue", out var bv) && bv.GetBoolean())
                    return "Этот ключ уже использован";

                var docName = doc.GetProperty("name").GetString();

                // Mark as used
                var updateUrl = $"https://firestore.googleapis.com/v1/{docName}?updateMask.fieldPaths=used&updateMask.fieldPaths=activated_at&updateMask.fieldPaths=machine_id";
                var updateBody = JsonSerializer.Serialize(new
                {
                    fields = new
                    {
                        used = new { booleanValue = true },
                        activated_at = new { stringValue = DateTime.UtcNow.ToString("o") },
                        machine_id = new { stringValue = MachineId }
                    }
                });

                var updateRequest = new HttpRequestMessage(HttpMethod.Patch, updateUrl);
                updateRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                updateRequest.Content = new StringContent(updateBody, Encoding.UTF8, "application/json");

                var updateResp = await _http.SendAsync(updateRequest);
                if (!updateResp.IsSuccessStatusCode)
                {
                    var errBody = await updateResp.Content.ReadAsStringAsync();
                    return $"Ошибка обновления ключа ({updateResp.StatusCode}): {errBody}";
                }

                // Save activation locally
                var dir = Path.GetDirectoryName(ActFile);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir!);
                await File.WriteAllTextAsync(ActFile, $"{key}|{MachineId}");

                return null; // success
            }
            catch (Exception ex)
            {
                return $"Ошибка: {ex.Message}";
            }
        }
    }
}
