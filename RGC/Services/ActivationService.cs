using System;
using System.IO;
using System.Threading.Tasks;
using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Firestore;
using Google.Cloud.Firestore.V1;

namespace RGC.Services
{
    public static class ActivationService
    {
        private static FirestoreDb? _db;
        private static bool _initialized;

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

        private static async Task InitFirebaseAsync()
        {
            if (_initialized) return;
            _initialized = true;

            var jsonPath = Path.Combine(AppContext.BaseDirectory, "firebase-key.json");
            if (!File.Exists(jsonPath))
                throw new FileNotFoundException("firebase-key.json не найден");

            if (FirebaseApp.DefaultInstance == null)
            {
                FirebaseApp.Create(new AppOptions
                {
                    Credential = GoogleCredential.FromFile(jsonPath)
                });
            }

            _db = await FirestoreDb.CreateAsync("affable-framing-502412-k6");
        }

        public static async Task<string?> ActivateAsync(string key)
        {
            try
            {
                await InitFirebaseAsync();
                if (_db == null) return "Ошибка подключения к Firebase";

                var snapshot = await _db.Collection("activation_keys")
                    .WhereEqualTo("key", key.Trim())
                    .Limit(1)
                    .GetSnapshotAsync();

                if (snapshot.Count == 0)
                    return "Ключ не найден";

                var doc = snapshot[0];
                var used = doc.TryGetValue<bool>("used", out var isUsed) && isUsed;

                if (used)
                    return "Этот ключ уже использован";

                // Mark as used
                await doc.Reference.UpdateAsync(new Dictionary<string, object>
                {
                    { "used", true },
                    { "activated_at", DateTime.UtcNow.ToString("o") },
                    { "machine_id", MachineId }
                });

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
