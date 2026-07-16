using System.IO;
using System.Text.Json;
using Microsoft.Win32;

namespace RGC.Services
{
    public static class SettingsService
    {
        private static readonly string DataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RGC");
        private static readonly string SettingsFile = Path.Combine(DataDir, "settings.json");

        private static SettingsData _data = Load();

        private class SettingsData
        {
            public string Theme { get; set; } = "Dark";
            public bool AutoStart { get; set; }
            public bool ConfirmExit { get; set; } = true;
            public bool MinimizeToTray { get; set; }
            public string Language { get; set; } = "RU";
            public int NotificationDuration { get; set; } = 3000;
            public bool NotificationSound { get; set; } = true;
        }

        private static SettingsData Load()
        {
            try
            {
                if (File.Exists(SettingsFile))
                {
                    var data = JsonSerializer.Deserialize<SettingsData>(File.ReadAllText(SettingsFile));
                    if (data != null)
                    {
                        if (data.NotificationDuration < 500) data.NotificationDuration = 3000;
                        if (string.IsNullOrEmpty(data.Theme)) data.Theme = "Dark";
                        return data;
                    }
                }
            }
            catch { }
            return new();
        }

        private static void Save()
        {
            try
            {
                Directory.CreateDirectory(DataDir);
                File.WriteAllText(SettingsFile, JsonSerializer.Serialize(_data, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { }
        }

        // --- Registry-based AutoStart (keep existing) ---
        public static bool AutoStart
        {
            get
            {
                try
                {
                    using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", false);
                    return key?.GetValue("RGC") != null;
                }
                catch { return false; }
            }
            set
            {
                try
                {
                    using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
                    if (value)
                        key?.SetValue("RGC", Path.Combine(AppContext.BaseDirectory, "RGC.exe"));
                    else
                        key?.DeleteValue("RGC", false);
                }
                catch { }
            }
        }

        // --- JSON-stored settings ---
        public static string Theme
        {
            get => _data.Theme;
            set { _data.Theme = value; Save(); }
        }

        public static bool ConfirmExit
        {
            get => _data.ConfirmExit;
            set { _data.ConfirmExit = value; Save(); }
        }

        public static bool MinimizeToTray
        {
            get => _data.MinimizeToTray;
            set { _data.MinimizeToTray = value; Save(); }
        }

        public static string Language
        {
            get => _data.Language;
            set { _data.Language = value; Save(); }
        }

        public static int NotificationDuration
        {
            get => _data.NotificationDuration;
            set { _data.NotificationDuration = value; Save(); }
        }

        public static bool NotificationSound
        {
            get => _data.NotificationSound;
            set { _data.NotificationSound = value; Save(); }
        }
    }
}
