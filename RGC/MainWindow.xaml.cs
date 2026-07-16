using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using RGC.Models;
using RGC.Services;
using MessageBox = System.Windows.MessageBox;
using ICSharpCode.AvalonEdit;

namespace RGC
{
    public partial class MainWindow : Window
    {
        private readonly DayZConfigService _configService = new();
        private ServerConfig? _currentConfig;

        private readonly ObservableCollection<ModItem> _modList = new();
        private readonly ObservableCollection<ModItem> _serverModList = new();

        private static readonly string[] MissionTemplates =
        {
            "dayzOffline.chernarusplus",
            "dayzOffline.enoch",
            "dayzOffline.sakhal",
            "dayzOffline.namalsk",
            "dayzOffline.deerisle",
            "dayzOffline.banov",
            "dayzOffline.chiemsee",
            "dayzOffline.esbek",
            "dayzOffline.takistan",
            "dayzOffline.ivlivs",
        };

        public MainWindow()
        {
            InitializeComponent();

            SetupTrayIcon();

            // Load from project if opened from ProjectLibraryWindow
            var project = ProjectLibraryWindow.LastOpenedProject;
            if (project != null)
            {
                ProjectTitleText.Text = project.Name;
                if (!string.IsNullOrEmpty(project.ServerPath) && Directory.Exists(project.ServerPath))
                {
                    ServerPathBox.Text = project.ServerPath;
                    LoadConfig();
                    ScanModsFromPath();
                    RefreshProfiles();
                }
            }

            MissionCombo.ItemsSource = MissionTemplates;
            ModListBox.ItemsSource = _modList;
            ServerModListBox.ItemsSource = _serverModList;
            Closing += (_, _) =>
            {
                SaveLastServerPath();
                _serverRunner.Dispose();
            };

            // Only load last path if no project was specified
            if (project == null)
            {
                LoadLastServerPath();
            }

            CheckInternetAccess();
            SteamCmdService.RefreshDetection();
            ApplyLang();

            _onServerLogChanged = (_, _) =>
            {
                if (_serverRunner.Log.Count > 0)
                    LogListBox.ScrollIntoView(_serverRunner.Log[^1]);
            };
            _onRconLogChanged = (_, _) =>
            {
                if (_rcon.Log.Count > 0)
                    RConLogBox.ScrollIntoView(_rcon.Log[^1]);
            };
        }

        private void ApplyLang()
        {
            var tabs = new[] {
                RGC.Services.Localization.T("tab.main"),
                RGC.Services.Localization.T("tab.world"),
                RGC.Services.Localization.T("tab.network"),
                RGC.Services.Localization.T("tab.mods"),
                RGC.Services.Localization.T("tab.batch"),
                RGC.Services.Localization.T("tab.modconfig"),
                RGC.Services.Localization.T("tab.launch"),
                RGC.Services.Localization.T("tab.rcon"),
                RGC.Services.Localization.T("tab.stats"),
            };
            for (var i = 0; i < tabs.Length && i < MainTabControl.Items.Count; i++)
                if (MainTabControl.Items[i] is System.Windows.Controls.TabItem ti)
                    ti.Header = tabs[i];

            if (CfgMainTitle != null)
                CfgMainTitle.Text = RGC.Services.Localization.T("cfg.main_title");
            if (StatusBar != null)
                StatusBar.Text = RGC.Services.Localization.T("settings.general") + " — RGC";

            StartServerBtn.Content = RGC.Services.Localization.T("launch.start");
            StopServerBtn.Content = RGC.Services.Localization.T("launch.stop");
            ClearLogBtn.Content = RGC.Services.Localization.T("launch.clear");
            InstallServerBtn.Content = RGC.Services.Localization.T("launch.install");
            BrowseFolderBtn.Content = RGC.Services.Localization.T("launch.browse");
            LoadConfigBtn.Content = RGC.Services.Localization.T("config.load");
            SaveConfigBtn.Content = RGC.Services.Localization.T("config.save");
            ScanModsBtn.Content = RGC.Services.Localization.T("config.scan_mods");
            RConConnectBtn.Content = RGC.Services.Localization.T("rcon.connect");
            RConDisconnectBtn.Content = RGC.Services.Localization.T("rcon.disconnect");
            RConSendBtn.Content = RGC.Services.Localization.T("rcon.send");
            ServerPathLabel.Text = RGC.Services.Localization.T("launch.path");
            CheckVersionBtn.Content = RGC.Services.Localization.T("launch.check_version");
            ExportConfigBtn.Content = RGC.Services.Localization.T("config.export");
            ImportConfigBtn.Content = RGC.Services.Localization.T("config.import");
        }

        private async void CheckInternetAccess()
        {
            try
            {
                using var ping = new System.Net.NetworkInformation.Ping();
                var reply = await ping.SendPingAsync("8.8.8.8", 3000);
                if (reply.Status == System.Net.NetworkInformation.IPStatus.Success)
                    NotificationService.Show("✅ Интернет подключён", 2500);
                else
                    NotificationService.Show("❌ Нет доступа к интернету", 3000);
            }
            catch
            {
                NotificationService.Show("❌ Нет доступа к интернету", 3000);
            }
        }

        private static string LastServerPathFile =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RGC", "lastserver.dat");

        private void LoadLastServerPath()
        {
            try
            {
                var file = LastServerPathFile;
                if (File.Exists(file))
                {
                    var path = File.ReadAllText(file).Trim();
                    if (Directory.Exists(path))
                    {
                        ServerPathBox.Text = path;
                        LoadConfig();
                        ScanModsFromPath();
                        RefreshProfiles();
                    }
                }
            }
            catch { /* ignore */ }
        }

        private void SaveLastServerPath()
        {
            try
            {
                var dir = Path.GetDirectoryName(LastServerPathFile);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir!);
                File.WriteAllText(LastServerPathFile, ServerPathBox.Text);
            }
            catch { /* ignore */ }
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                DragMove();
        }

        private void SetupTrayIcon()
        {
            try
            {
                if (_trayIcon != null) { _trayRefCount++; return; }
                _trayRefCount = 1;
                _trayIcon = new System.Windows.Forms.NotifyIcon
                {
                    Icon = System.Drawing.Icon.ExtractAssociatedIcon(
                        System.Windows.Forms.Application.ExecutablePath),
                    Text = "RGC DayZ Configurator",
                    Visible = true
                };
                _trayIcon.Click += (_, _) =>
            {
                Show();
                WindowState = System.Windows.WindowState.Normal;
                Activate();
            };

            var menu = new System.Windows.Forms.ContextMenuStrip();
            menu.Items.Add("Показать RGC", null, (_, _) =>
            {
                Show();
                WindowState = System.Windows.WindowState.Normal;
                Activate();
            });
            menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
            menu.Items.Add("Выйти", null, (_, _) =>
            {
                _trayIcon!.Visible = false;
                _trayIcon.Dispose();
                _trayIcon = null;
                _trayRefCount = 0;
                System.Windows.Application.Current.Shutdown();
            });
            _trayIcon.ContextMenuStrip = menu;

            StateChanged += (_, _) =>
            {
                if (WindowState == System.Windows.WindowState.Minimized && SettingsService.MinimizeToTray)
                    Hide();
            };
            }
            catch { }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            if (SettingsService.MinimizeToTray)
            {
                Hide();
                return;
            }

            if (SettingsService.ConfirmExit)
            {
                var result = System.Windows.MessageBox.Show(
                    "Вы действительно хотите выйти?",
                    "Выход",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Question);
                if (result != System.Windows.MessageBoxResult.Yes)
                    return;
            }

            _trayRefCount--;
            if (_trayRefCount <= 0)
            {
                _trayIcon?.Dispose();
                _trayIcon = null;
            }
            System.Windows.Application.Current.Shutdown();
        }

        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = System.Windows.WindowState.Minimized;
        }

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            var settings = new SettingsWindow();
            settings.Owner = this;
            settings.ShowDialog();
        }

        private void BackToProjects_Click(object sender, RoutedEventArgs e)
        {
            SaveCurrentProject();
            var lib = new ProjectLibraryWindow();
            lib.Show();
            Close();
        }

        private void SaveCurrentProject()
        {
            var project = ProjectLibraryWindow.LastOpenedProject;
            if (project == null) return;
            project.LastOpenedAt = DateTime.Now;
            if (!string.IsNullOrEmpty(ServerPathBox.Text))
                project.ServerPath = ServerPathBox.Text;
            ProjectService.Update(project);
        }

        private void BrowseFolder_Click(object sender, RoutedEventArgs e)
        {
            using var dialog = new System.Windows.Forms.FolderBrowserDialog();
            dialog.Description = "Выберите папку с DayZ сервером";
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                ServerPathBox.Text = dialog.SelectedPath;
                SaveLastServerPath();
                SaveCurrentProject();
                LoadConfig();
                ScanMods_Click(sender, e);
                RefreshProfiles();
            }
        }

        private void ScanMods_Click(object sender, RoutedEventArgs e)
        {
            ScanModsFromPath();
        }

        private void ScanModsFromPath()
        {
            if (string.IsNullOrWhiteSpace(ServerPathBox.Text) || !Directory.Exists(ServerPathBox.Text))
            {
                MessageBox.Show("Сначала выберите папку сервера.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var allMods = Directory.GetDirectories(ServerPathBox.Text)
                .Where(d => Path.GetFileName(d).StartsWith("@"))
                .Select(d => Path.GetFileName(d))
                .OrderBy(n => n)
                .ToList();

            if (allMods.Count == 0)
            {
                MessageBox.Show("Папки с модами (начинающиеся с @) не найдены.", "Инфо", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            _modList.Clear();
            _serverModList.Clear();

            foreach (var mod in allMods)
            {
                var item = new ModItem { Name = mod };
                if (mod.StartsWith("@Servermod", StringComparison.OrdinalIgnoreCase) ||
                    mod.StartsWith("@ServerMod", StringComparison.OrdinalIgnoreCase))
                    _serverModList.Add(item);
                else
                    _modList.Add(item);
            }

            StatusBar.Text = $"Найдено модов: {_modList.Count}, серверных модов: {_serverModList.Count}";
            NotificationService.Show($"Сканирование завершено: {_modList.Count} модов, {_serverModList.Count} серверных", 2500);
            LoadModVersions();
        }

        private void LoadConfig_Click(object sender, RoutedEventArgs e)
        {
            LoadConfig();
        }

        private void LoadConfig()
        {
            if (string.IsNullOrWhiteSpace(ServerPathBox.Text))
            {
                MessageBox.Show("Сначала выберите папку сервера.", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!Directory.Exists(ServerPathBox.Text))
            {
                MessageBox.Show("Папка не найдена.", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                _currentConfig = _configService.ReadConfig(ServerPathBox.Text);
                ApplyConfigToUI(_currentConfig);
                LoadModVersions();
                StatusBar.Text = $"Конфиг загружен: {ServerPathBox.Text}";
                NotificationService.Show("Конфиг загружен", 2000);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveConfig_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(ServerPathBox.Text))
            {
                MessageBox.Show("Сначала выберите папку сервера.", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                _currentConfig = CollectConfigFromUI();
                _configService.WriteConfig(_currentConfig);
                StatusBar.Text = $"Конфиг сохранён в: {ServerPathBox.Text}";
                NotificationService.Show("Конфигурация сохранена", 2000);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка сохранения: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // --- Mod list handlers ---

        private void AddMod_Click(object sender, RoutedEventArgs e)
        {
            AddItemToList(NewModBox, _modList);
        }

        private void AddServerMod_Click(object sender, RoutedEventArgs e)
        {
            AddItemToList(NewServerModBox, _serverModList);
        }

        private void AddItemToList(System.Windows.Controls.TextBox box, ObservableCollection<ModItem> list)
        {
            var text = box.Text.Trim();
            if (string.IsNullOrWhiteSpace(text) || text == "Введите название мода...") return;
            if (!list.Any(i => i.Name == text))
                list.Add(new ModItem { Name = text });
            box.Text = "Введите название мода...";
        }

        private void RemoveMod_Click(object sender, RoutedEventArgs e)
        {
            RemoveSelected(ModListBox, _modList);
        }

        private void RemoveServerMod_Click(object sender, RoutedEventArgs e)
        {
            RemoveSelected(ServerModListBox, _serverModList);
        }

        private void RemoveSelected(System.Windows.Controls.ListBox listBox, ObservableCollection<ModItem> list)
        {
            if (listBox.SelectedItem is ModItem item)
                list.Remove(item);
        }

        private void OpenModFolder_Click(object sender, RoutedEventArgs e)
        {
            OpenFolderForItem(sender, e);
        }

        private void OpenServerModFolder_Click(object sender, RoutedEventArgs e)
        {
            OpenFolderForItem(sender, e);
        }

        private void DeleteModFolder_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.DataContext is ModItem modItem)
            {
                var modName = modItem.Name;
                var result = MessageBox.Show(
                    $"Удалить папку мода «{modName}» и убрать его из конфига?\n\nПуть: {Path.Combine(ServerPathBox.Text, modName)}",
                    "Подтверждение удаления",
                    MessageBoxButton.YesNo, MessageBoxImage.Warning);

                if (result != MessageBoxResult.Yes) return;

                // Delete folder
                var fullPath = Path.Combine(ServerPathBox.Text, modName);
                try
                {
                    if (Directory.Exists(fullPath))
                        Directory.Delete(fullPath, true);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Не удалось удалить папку:\n{ex.Message}", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Remove from list
                _modList.Remove(modItem);
                _serverModList.Remove(modItem);
                NotificationService.Show($"Мод «{modName}» удалён", 2500);
            }
        }

        private void OpenFolderForItem(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.DataContext is ModItem modItem)
            {
                var fullPath = Path.Combine(ServerPathBox.Text, modItem.Name);
                if (Directory.Exists(fullPath))
                    System.Diagnostics.Process.Start("explorer.exe", fullPath);
                else
                    MessageBox.Show($"Папка не найдена:\n{fullPath}", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void OpenWorkshopFromMod_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.DataContext is ModItem modItem)
            {
                var metaFile = Path.Combine(ServerPathBox.Text, modItem.Name, "meta.cpp");
                if (!File.Exists(metaFile))
                {
                    NotificationService.Show("meta.cpp не найден", 2000);
                    return;
                }

                try
                {
                    var content = File.ReadAllText(metaFile);
                    var match = System.Text.RegularExpressions.Regex.Match(content, @"publishedid\s*=\s*(\d+)",
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                    if (!match.Success)
                    {
                        NotificationService.Show("PublishedID не найден в meta.cpp", 2000);
                        return;
                    }

                    var pubId = match.Groups[1].Value;
                    var url = $"https://steamcommunity.com/sharedfiles/filedetails/?id={pubId}";
                    Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка чтения meta.cpp:\n{ex.Message}", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void LoadModVersions()
        {
            if (string.IsNullOrWhiteSpace(ServerPathBox.Text) || !Directory.Exists(ServerPathBox.Text))
                return;

            var all = _modList.Concat(_serverModList).ToList();
            if (all.Count == 0) return;

            var serverPath = ServerPathBox.Text;

            Task.Run(() =>
            {
                foreach (var item in all)
                {
                    var metaFile = Path.Combine(serverPath, item.Name, "meta.cpp");
                    if (!File.Exists(metaFile)) continue;

                    try
                    {
                        var content = File.ReadAllText(metaFile);
                        var match = System.Text.RegularExpressions.Regex.Match(content,
                            @"version\s*=\s*""?([^""\s;]+)""?",
                            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                        if (match.Success)
                            Dispatcher.Invoke(() => item.Version = match.Groups[1].Value);
                    }
                    catch { }
                }
            });
        }

        private void RemoveModPlaceholder(object sender, System.Windows.Input.KeyboardFocusChangedEventArgs e)
        {
            if (NewModBox.Text == "Введите название мода...")
                NewModBox.Text = "";
        }

        private void SetModPlaceholder(object sender, System.Windows.Input.KeyboardFocusChangedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(NewModBox.Text))
                NewModBox.Text = "Введите название мода...";
        }

        private void RemoveSrvModPlaceholder(object sender, System.Windows.Input.KeyboardFocusChangedEventArgs e)
        {
            if (NewServerModBox.Text == "Введите название мода...")
                NewServerModBox.Text = "";
        }

        private void SetSrvModPlaceholder(object sender, System.Windows.Input.KeyboardFocusChangedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(NewServerModBox.Text))
                NewServerModBox.Text = "Введите название мода...";
        }

        // --- UI <-> Config mapping ---

        private void ApplyConfigToUI(ServerConfig c)
        {
            HostnameBox.Text = c.Hostname;
            PasswordBox.Text = c.Password;
            AdminPasswordBox.Text = c.PasswordAdmin;
            DescriptionBox.Text = c.Description;
            MaxPlayersBox.Text = c.MaxPlayers.ToString();
            WhitelistCheck.IsChecked = c.EnableWhitelist;
            ShardIdBox.Text = c.ShardId;
            MissionCombo.Text = c.MissionTemplate;

            Disable3rdPersonCheck.IsChecked = c.Disable3rdPerson;
            DisableCrosshairCheck.IsChecked = c.DisableCrosshair;
            DisablePersonalLightCheck.IsChecked = c.DisablePersonalLight;
            LightingBox.Text = c.LightingConfig.ToString();
            ServerTimeBox.Text = c.ServerTime;
            TimeAccelBox.Text = c.ServerTimeAcceleration.ToString("0.#");
            NightAccelBox.Text = c.ServerNightTimeAcceleration.ToString("0.#");
            TimePersistentCheck.IsChecked = c.ServerTimePersistent;

            PortBox.Text = c.ServerPort.ToString();
            VerifySigBox.Text = c.VerifySignatures.ToString();
            ForceSameBuildCheck.IsChecked = c.ForceSameBuild;
            DisableVonCheck.IsChecked = c.DisableVoN;
            VonQualityBox.Text = c.VonCodecQuality.ToString();
            LoginConcurrentBox.Text = c.LoginQueueConcurrentPlayers.ToString();
            LoginMaxBox.Text = c.LoginQueueMaxPlayers.ToString();
            InstanceIdBox.Text = c.InstanceId.ToString();
            StorageAutoFixCheck.IsChecked = c.StorageAutoFix;
            RConPassBox.Text = c.RConPassword;

            _modList.Clear();
            foreach (var m in SplitModList(c.ModList))
                _modList.Add(new ModItem { Name = m });

            _serverModList.Clear();
            foreach (var m in SplitModList(c.ServerModList))
                _serverModList.Add(new ModItem { Name = m });

            BatchNameBox.Text = c.BatchServerName;
            BatchPortBox.Text = c.ServerPort.ToString();
            CpuBox.Text = c.CpuCores.ToString();
            ConfigFileBox.Text = c.ConfigFileName;
            RestartBox.Text = c.RestartInterval.ToString();
        }

        private ServerConfig CollectConfigFromUI()
        {
            return new ServerConfig
            {
                ServerLocation = ServerPathBox.Text,

                Hostname = HostnameBox.Text,
                Password = PasswordBox.Text,
                PasswordAdmin = AdminPasswordBox.Text,
                Description = DescriptionBox.Text,
                MaxPlayers = int.TryParse(MaxPlayersBox.Text, out var mp) ? mp : 60,
                EnableWhitelist = WhitelistCheck.IsChecked == true,
                ShardId = ShardIdBox.Text,
                MissionTemplate = MissionCombo.Text,

                Disable3rdPerson = Disable3rdPersonCheck.IsChecked == true,
                DisableCrosshair = DisableCrosshairCheck.IsChecked == true,
                DisablePersonalLight = DisablePersonalLightCheck.IsChecked == true,
                LightingConfig = int.TryParse(LightingBox.Text, out var lc) ? lc : 0,
                ServerTime = ServerTimeBox.Text,
                ServerTimeAcceleration = double.TryParse(TimeAccelBox.Text, out var ta) ? ta : 12,
                ServerNightTimeAcceleration = double.TryParse(NightAccelBox.Text, out var na) ? na : 1,
                ServerTimePersistent = TimePersistentCheck.IsChecked == true,

                ServerPort = int.TryParse(PortBox.Text, out var sp) ? sp : 2302,
                VerifySignatures = int.TryParse(VerifySigBox.Text, out var vs) ? vs : 2,
                ForceSameBuild = ForceSameBuildCheck.IsChecked == true,
                DisableVoN = DisableVonCheck.IsChecked == true,
                VonCodecQuality = int.TryParse(VonQualityBox.Text, out var vcq) ? vcq : 20,
                LoginQueueConcurrentPlayers = int.TryParse(LoginConcurrentBox.Text, out var lqcp) ? lqcp : 5,
                LoginQueueMaxPlayers = int.TryParse(LoginMaxBox.Text, out var lqmp) ? lqmp : 500,
                InstanceId = int.TryParse(InstanceIdBox.Text, out var iid) ? iid : 1,
                StorageAutoFix = StorageAutoFixCheck.IsChecked == true,
                RConPassword = RConPassBox.Text,

                ModList = string.Join(";", _modList.Select(m => m.Name)),
                ServerModList = string.Join(";", _serverModList.Select(m => m.Name)),

                BatchServerName = BatchNameBox.Text,
                CpuCores = int.TryParse(CpuBox.Text, out var cpu) ? cpu : 2,
                ConfigFileName = ConfigFileBox.Text,
                RestartInterval = int.TryParse(RestartBox.Text, out var ri) ? ri : 14390,
            };
        }

        private static IEnumerable<string> SplitModList(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return Array.Empty<string>();
            return input.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        // --- Profiles / Config модов tab ---

        private readonly ObservableCollection<TreeItem> _profileTreeRoots = new();
        private string? _currentProfileFile;

        private void RefreshProfiles_Click(object sender, RoutedEventArgs e)
        {
            RefreshProfiles();
        }

        private void RefreshProfiles()
        {
            var profilesDir = Path.Combine(ServerPathBox.Text, "profiles");
            if (!Directory.Exists(profilesDir))
            {
                NotificationService.Show("Папка profiles не найдена", 2000);
                return;
            }

            _profileTreeRoots.Clear();
            foreach (var dir in Directory.GetDirectories(profilesDir).OrderBy(d => Path.GetFileName(d)))
            {
                var root = new TreeItem
                {
                    Name = Path.GetFileName(dir),
                    FullPath = dir,
                    IsFile = false
                };
                BuildTree(root);
                _profileTreeRoots.Add(root);
            }

            ProfilesTree.ItemsSource = _profileTreeRoots;
            ModConfigEditor.Visibility = Visibility.Collapsed;
            ProfileFilePathText.Text = "Выберите файл";
            SaveProfileConfigBtn.IsEnabled = false;
            _currentProfileFile = null;

            if (_profileTreeRoots.Count == 0)
                NotificationService.Show("В profiles нет папок", 2000);
            else
                NotificationService.Show($"Найдено {_profileTreeRoots.Count} папок конфигов", 2000);
        }

        private static void BuildTree(TreeItem parent)
        {
            try
            {
                foreach (var dir in Directory.GetDirectories(parent.FullPath).OrderBy(d => Path.GetFileName(d)))
                {
                    var item = new TreeItem
                    {
                        Name = Path.GetFileName(dir),
                        FullPath = dir,
                        IsFile = false
                    };
                    BuildTree(item);
                    parent.Children.Add(item);
                }
                foreach (var file in Directory.GetFiles(parent.FullPath).OrderBy(f => Path.GetFileName(f)))
                {
                    parent.Children.Add(new TreeItem
                    {
                        Name = Path.GetFileName(file),
                        FullPath = file,
                        IsFile = true
                    });
                }
            }
            catch { }
        }

        private void ProfilesTree_SelectedItemChanged(object sender, System.Windows.RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue is not TreeItem item) return;

            if (!item.IsFile)
            {
                // Folder selected — expand/collapse, no file to show
                ModConfigEditor.Visibility = Visibility.Collapsed;
                ProfileFilePathText.Text = item.FullPath;
                SaveProfileConfigBtn.IsEnabled = false;
                _currentProfileFile = null;
                return;
            }

            // File selected
            var filePath = item.FullPath;
            if (!File.Exists(filePath)) return;

            try
            {
                var content = File.ReadAllText(filePath);
                ModConfigEditor.Text = content;
                ModConfigEditor.Visibility = Visibility.Visible;
                ProfileFilePathText.Text = filePath;
                _currentProfileFile = filePath;
                SaveProfileConfigBtn.IsEnabled = true;

                var ext = Path.GetExtension(item.Name).ToLowerInvariant();
                ModConfigEditor.SyntaxHighlighting = ext switch
                {
                    ".xml" => ICSharpCode.AvalonEdit.Highlighting.HighlightingManager.Instance.GetDefinition("XML"),
                    ".json" => ICSharpCode.AvalonEdit.Highlighting.HighlightingManager.Instance.GetDefinition("JavaScript"),
                    _ => null
                };
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка чтения файла:\n{ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveConfigFile_Click(object sender, RoutedEventArgs e)
        {
            if (_currentProfileFile == null) return;

            try
            {
                File.WriteAllText(_currentProfileFile, ModConfigEditor.Text);
                NotificationService.Show("Файл сохранён", 2000);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка сохранения:\n{ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void CheckVersion_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(ServerPathBox.Text) || !System.IO.Directory.Exists(ServerPathBox.Text))
            {
                System.Windows.MessageBox.Show("Сначала выберите папку сервера.", "Ошибка",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            CheckVersionBtn.IsEnabled = false;
            CheckVersionBtn.Content = "⏳ ...";
            StatusBar.Text = "Проверка версии...";

            var result = await RGC.Services.DayZVersionChecker.CheckVersionAsync(ServerPathBox.Text);
            CheckVersionBtn.IsEnabled = true;
            ApplyLang();

            if (result.installed == null)
            {
                NotificationService.Show("Не удалось определить версию DayZServer_x64.exe", 4000);
                return;
            }

            if (result.latest == null)
            {
                NotificationService.Show($"Установлена версия {result.installed}. Не удалось проверить актуальную в Steam.", 4000);
                return;
            }

            if (result.isLatest == true)
            {
                NotificationService.Show($"✅ Версия {result.installed} — актуальная", 3000);
                StatusBar.Text = $"DayZ {result.installed} — актуальная";
            }
            else
            {
                NotificationService.Show($"⚠ Доступно обновление: {result.latest} (у вас {result.installed})", 5000);
                StatusBar.Text = $"DayZ {result.installed} → {result.latest}";
            }
        }

        private void ExportConfig_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            var config = CollectConfigFromUI();
            var dialog = new System.Windows.Forms.SaveFileDialog
            {
                Title = "Экспорт конфигурации",
                Filter = "RGC Config (*.rgc)|*.rgc|JSON (*.json)|*.json",
                FileName = "server_config.rgc"
            };
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                try
                {
                    _configService.ExportConfig(config, dialog.FileName);
                    NotificationService.Show("Конфигурация экспортирована", 2000);
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Ошибка экспорта:\n{ex.Message}", "Ошибка",
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
            }
        }

        private void ImportConfig_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.OpenFileDialog
            {
                Title = "Импорт конфигурации",
                Filter = "RGC Config (*.rgc)|*.rgc|JSON (*.json)|*.json"
            };
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                try
                {
                    var config = _configService.ImportConfig(dialog.FileName);
                    if (config == null)
                    {
                        NotificationService.Show("Не удалось прочитать файл конфигурации", 3000);
                        return;
                    }
                    ApplyConfigToUI(config);
                    _currentConfig = config;
                    NotificationService.Show("Конфигурация импортирована", 2000);

                    if (!string.IsNullOrEmpty(config.ServerLocation) && System.IO.Directory.Exists(config.ServerLocation))
                    {
                        ServerPathBox.Text = config.ServerLocation;
                        SaveLastServerPath();
                        ScanMods_Click(sender, e);
                        RefreshProfiles();
                    }
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Ошибка импорта:\n{ex.Message}", "Ошибка",
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
            }
        }

        // --- Server runner tab ---

        private readonly ServerRunner _serverRunner = new();

        private void StartServer_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(ServerPathBox.Text) || !Directory.Exists(ServerPathBox.Text))
            {
                System.Windows.MessageBox.Show("Сначала выберите папку сервера.", "Ошибка",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            var errors = ValidateServerPaths(ServerPathBox.Text);
            if (errors.Count > 0)
            {
                foreach (var err in errors)
                    NotificationService.Show(err, 4000);
                return;
            }

            var config = CollectConfigFromUI();
            LogListBox.ItemsSource = _serverRunner.Log;

            _serverRunner.Log.CollectionChanged -= _onServerLogChanged;
            _serverRunner.Log.CollectionChanged += _onServerLogChanged;

            _serverRunner.Start(
                ServerPathBox.Text,
                config.ConfigFileName,
                config.ServerPort,
                config.CpuCores,
                config.BatchServerName,
                config.ModList,
                config.ServerModList
            );

            StartServerBtn.IsEnabled = false;
            StopServerBtn.IsEnabled = true;
            StatusBar.Text = "Сервер запущен";
        }

        private static List<string> ValidateServerPaths(string serverDir)
        {
            var errors = new List<string>();

            var exe = System.IO.Path.Combine(serverDir, "DayZServer_x64.exe");
            if (!System.IO.File.Exists(exe))
                errors.Add("❌ DayZServer_x64.exe не найден");

            var cfg = System.IO.Path.Combine(serverDir, "serverDZ.cfg");
            if (!System.IO.File.Exists(cfg))
                errors.Add("❌ serverDZ.cfg не найден");

            var be = System.IO.Path.Combine(serverDir, "battleye");
            if (!System.IO.Directory.Exists(be))
                errors.Add("❌ Папка battleye не найдена");

            var profiles = System.IO.Path.Combine(serverDir, "profiles");
            if (!System.IO.Directory.Exists(profiles))
                errors.Add("❌ Папка profiles не найдена");

            return errors;
        }

        private void StopServer_Click(object sender, RoutedEventArgs e)
        {
            _serverRunner.Stop();
            StartServerBtn.IsEnabled = true;
            StopServerBtn.IsEnabled = false;
            StatusBar.Text = "Сервер остановлен";
        }

        private void GeneratePassword_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            var btn = sender as System.Windows.Controls.Button;
            var target = btn?.Tag as string;
            var pass = GeneratorService.GeneratePassword(12);
            switch (target)
            {
                case "PasswordBox": PasswordBox.Text = pass; break;
                case "AdminPasswordBox": AdminPasswordBox.Text = pass; break;
            }
        }

        private void GenerateDescription_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            var name = string.IsNullOrWhiteSpace(HostnameBox.Text) ? null : HostnameBox.Text.Trim();
            DescriptionBox.Text = GeneratorService.GenerateDescription(name);
        }

        private void ClearLog_Click(object sender, RoutedEventArgs e)
        {
            _serverRunner.Log.Clear();
        }

        private readonly SteamCmdService _steamCmd = new();
        private readonly NotifyCollectionChangedEventHandler _onServerLogChanged;
        private readonly NotifyCollectionChangedEventHandler _onRconLogChanged;
        private bool _downloadNotified;
        private static System.Windows.Forms.NotifyIcon? _trayIcon;
        private static int _trayRefCount;

        private void InstallServer_Click(object sender, RoutedEventArgs e)
        {
            using var dialog = new System.Windows.Forms.FolderBrowserDialog();
            dialog.Description = "Выберите папку для установки DayZ сервера";
            dialog.ShowNewFolderButton = true;
            if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                return;

            var installDir = dialog.SelectedPath;

            // SteamCMD не поддерживает не-ASCII символы в пути
            if (installDir.Any(c => c > 127))
            {
                var result = MessageBox.Show(
                    "Путь содержит кириллицу или другие не-ASCII символы. SteamCMD не сможет установить сервер по этому пути.\n\n" +
                    "Хотите создать папку без кириллицы на рабочем столе (DayZServer)?",
                    "Неверный путь",
                    MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result == MessageBoxResult.Yes)
                {
                    installDir = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "DayZServer");
                    Directory.CreateDirectory(installDir);
                }
                else
                    return;
            }

            var steamLogin = PromptSteamLogin(this);
            if (steamLogin == null)
                return;

            LogListBox.ItemsSource = _serverRunner.Log;
            _steamCmd.Output -= OnSteamOutput;
            _steamCmd.Output += OnSteamOutput;

            _steamCmd.DownloadProgress -= OnDownloadProgress;
            _steamCmd.DownloadProgress += OnDownloadProgress;
            _downloadNotified = false;
            ProgressPanel.Visibility = Visibility.Visible;
            DownloadProgressBar.Value = 0;
            ProgressPercentText.Text = "0%";

            _steamCmd.InstallServer(installDir, steamLogin.Value.login, steamLogin.Value.password,
                () => System.Threading.Tasks.Task.FromResult(Dispatcher.Invoke(() => PromptSteamGuardCode(this))));
        }

        private static string? LoadRememberedLogin()
        {
            try
            {
                var file = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "RGC", "steam_login.dat");
                return File.Exists(file) ? File.ReadAllText(file).Trim() : null;
            }
            catch { return null; }
        }

        private static string? LoadRememberedPassword()
        {
            try
            {
                var file = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "RGC", "steam_pass.dat");
                if (!File.Exists(file)) return null;
                var encrypted = File.ReadAllText(file);
                var data = Convert.FromBase64String(encrypted);
                var plain = ProtectedData.Unprotect(data, null, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(plain);
            }
            catch { return null; }
        }

        private static void SaveRememberedCredentials(string login, string password)
        {
            try
            {
                var dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RGC");
                Directory.CreateDirectory(dir);

                File.WriteAllText(Path.Combine(dir, "steam_login.dat"), login);

                var plain = Encoding.UTF8.GetBytes(password);
                var encrypted = ProtectedData.Protect(plain, null, DataProtectionScope.CurrentUser);
                File.WriteAllText(Path.Combine(dir, "steam_pass.dat"), Convert.ToBase64String(encrypted));
            }
            catch { }
        }

        private static void ClearRememberedCredentials()
        {
            try
            {
                var dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RGC");
                foreach (var f in new[] { "steam_login.dat", "steam_pass.dat" })
                {
                    var path = Path.Combine(dir, f);
                    if (File.Exists(path)) File.Delete(path);
                }
            }
            catch { }
        }

        private static (string login, string password)? PromptSteamLogin(System.Windows.Window owner)
        {
            var innerBg = GetBrush("WindowInnerBgBrush", "#1e1e2e");
            var textBrush = GetBrush("TextBrush", "#cdd6f4");
            var inputBg = GetBrush("InputBgBrush", "#313244");
            var inputText = GetBrush("InputTextBrush", "#cdd6f4");
            var accent = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x89, 0xb4, 0xfa));
            var dimText = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x6c, 0x70, 0x8b));

            // --- Header accent bar ---
            var headerBar = new System.Windows.Controls.Border
            {
                Height = 4,
                Background = accent,
                BorderThickness = new System.Windows.Thickness(0)
            };

            var titleText = new System.Windows.Controls.TextBlock
            {
                Text = "Вход в Steam",
                FontSize = 20,
                FontWeight = System.Windows.FontWeights.SemiBold,
                Foreground = textBrush,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                Margin = new System.Windows.Thickness(0, 20, 0, 4)
            };
            var subtitleText = new System.Windows.Controls.TextBlock
            {
                Text = "для установки DayZ сервера",
                FontSize = 12,
                Foreground = dimText,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                Margin = new System.Windows.Thickness(0, 0, 0, 20)
            };

            // --- Fields ---
            var loginLabel = new System.Windows.Controls.TextBlock
                { Text = "Логин", FontSize = 12, Foreground = textBrush, Margin = new System.Windows.Thickness(0, 0, 0, 4) };
            var loginBox = new System.Windows.Controls.TextBox
            {
                FontSize = 14,
                Padding = new System.Windows.Thickness(10, 8, 10, 8),
                Background = inputBg,
                Foreground = inputText,
                BorderThickness = new System.Windows.Thickness(0),
                CaretBrush = textBrush
            };

            var passLabel = new System.Windows.Controls.TextBlock
                { Text = "Пароль", FontSize = 12, Foreground = textBrush, Margin = new System.Windows.Thickness(0, 12, 0, 4) };
            var passBox = new System.Windows.Controls.PasswordBox
            {
                FontSize = 14,
                Padding = new System.Windows.Thickness(10, 8, 10, 8),
                Background = inputBg,
                Foreground = inputText,
                BorderThickness = new System.Windows.Thickness(0)
            };

            // Pre-fill remembered credentials
            var rememberedLogin = LoadRememberedLogin();
            var rememberedPass = LoadRememberedPassword();
            if (rememberedLogin != null)
            {
                loginBox.Text = rememberedLogin;
                if (rememberedPass != null)
                    passBox.Password = rememberedPass;
            }

            // --- Remember checkbox ---
            var rememberBox = new System.Windows.Controls.CheckBox
            {
                Content = "Запомнить Steam аккаунт",
                FontSize = 12,
                Foreground = dimText,
                Margin = new System.Windows.Thickness(0, 14, 0, 0),
                IsChecked = rememberedLogin != null
            };

            // --- Buttons ---
            var okBtn = new System.Windows.Controls.Button
            {
                Content = "Войти",
                IsDefault = true,
                Height = 36,
                Margin = new System.Windows.Thickness(0, 18, 0, 0),
                Padding = new System.Windows.Thickness(20, 0, 20, 0),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch,
                Cursor = System.Windows.Input.Cursors.Hand,
                FontSize = 14,
                FontWeight = System.Windows.FontWeights.SemiBold,
                Background = accent,
                Foreground = System.Windows.Media.Brushes.White,
                BorderThickness = new System.Windows.Thickness(0)
            };

            var cancelBtn = new System.Windows.Controls.Button
            {
                Content = "Отмена",
                IsCancel = true,
                Height = 36,
                Margin = new System.Windows.Thickness(0, 6, 0, 0),
                Padding = new System.Windows.Thickness(20, 0, 20, 0),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch,
                Cursor = System.Windows.Input.Cursors.Hand,
                FontSize = 14,
                Background = System.Windows.Media.Brushes.Transparent,
                Foreground = dimText,
                BorderThickness = new System.Windows.Thickness(0)
            };
            cancelBtn.MouseEnter += (_, _) => cancelBtn.Foreground = textBrush;
            cancelBtn.MouseLeave += (_, _) => cancelBtn.Foreground = dimText;

            // --- Layout ---
            var fieldPanel = new System.Windows.Controls.StackPanel { Margin = new System.Windows.Thickness(24, 0, 24, 24) };
            fieldPanel.Children.Add(titleText);
            fieldPanel.Children.Add(subtitleText);
            fieldPanel.Children.Add(loginLabel);
            fieldPanel.Children.Add(loginBox);
            fieldPanel.Children.Add(passLabel);
            fieldPanel.Children.Add(passBox);
            fieldPanel.Children.Add(rememberBox);
            fieldPanel.Children.Add(okBtn);
            fieldPanel.Children.Add(cancelBtn);

            var outerStack = new System.Windows.Controls.StackPanel();
            outerStack.Children.Add(headerBar);
            outerStack.Children.Add(fieldPanel);

            var win = new System.Windows.Window
            {
                Title = "Авторизация Steam",
                Content = outerStack,
                Width = 360,
                SizeToContent = System.Windows.SizeToContent.Height,
                ResizeMode = System.Windows.ResizeMode.NoResize,
                WindowStartupLocation = System.Windows.WindowStartupLocation.CenterOwner,
                Owner = owner,
                FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
                Background = innerBg,
                Foreground = textBrush,
                WindowStyle = System.Windows.WindowStyle.None,
                AllowsTransparency = true,
                BorderThickness = new System.Windows.Thickness(0)
            };

            outerStack.MouseLeftButtonDown += (_, _) => win.DragMove();

            (string login, string password)? result = null;
            okBtn.Click += (_, _) =>
            {
                var l = loginBox.Text.Trim();
                var p = passBox.Password;
                if (string.IsNullOrEmpty(l) || string.IsNullOrEmpty(p))
                {
                    System.Windows.MessageBox.Show(win, "Введите логин и пароль.", "Ошибка",
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                    return;
                }
                if (rememberBox.IsChecked == true)
                    SaveRememberedCredentials(l, p);
                else
                    ClearRememberedCredentials();
                result = (l, p);
                win.DialogResult = true;
                win.Close();
            };

            win.ShowDialog();
            return result;
        }

        private static System.Windows.Media.Brush GetBrush(string key, string hex)
        {
            var fromRes = System.Windows.Application.Current.TryFindResource(key) as System.Windows.Media.Brush;
            if (fromRes != null) return fromRes;
            var r = byte.Parse(hex.Substring(1, 2), System.Globalization.NumberStyles.HexNumber);
            var g = byte.Parse(hex.Substring(3, 2), System.Globalization.NumberStyles.HexNumber);
            var b = byte.Parse(hex.Substring(5, 2), System.Globalization.NumberStyles.HexNumber);
            return new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(r, g, b));
        }

        public static string? PromptSteamGuardCode(System.Windows.Window owner)
        {
            var innerBg = GetBrush("WindowInnerBgBrush", "#1e1e2e");
            var textBrush = GetBrush("TextBrush", "#cdd6f4");
            var inputBg = GetBrush("InputBgBrush", "#313244");
            var inputText = GetBrush("InputTextBrush", "#cdd6f4");
            var accent = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x89, 0xb4, 0xfa));
            var dimText = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x6c, 0x70, 0x8b));

            // --- Header accent ---
            var headerBar = new System.Windows.Controls.Border
            {
                Height = 4,
                Background = accent,
                BorderThickness = new System.Windows.Thickness(0)
            };

            var titleText = new System.Windows.Controls.TextBlock
            {
                Text = "Steam Guard",
                FontSize = 20,
                FontWeight = System.Windows.FontWeights.SemiBold,
                Foreground = textBrush,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                Margin = new System.Windows.Thickness(0, 20, 0, 8)
            };

            // --- Input ---
            var box = new System.Windows.Controls.TextBox
            {
                FontSize = 16,
                Padding = new System.Windows.Thickness(10, 8, 10, 8),
                Background = inputBg,
                Foreground = inputText,
                BorderThickness = new System.Windows.Thickness(0),
                CaretBrush = textBrush,
                TextAlignment = System.Windows.TextAlignment.Center,
                HorizontalContentAlignment = System.Windows.HorizontalAlignment.Center
            };

            // --- Buttons ---
            var okBtn = new System.Windows.Controls.Button
            {
                Content = "Подтвердить",
                IsDefault = true,
                Height = 36,
                Margin = new System.Windows.Thickness(0, 12, 0, 0),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch,
                Cursor = System.Windows.Input.Cursors.Hand,
                FontSize = 14,
                FontWeight = System.Windows.FontWeights.SemiBold,
                Background = accent,
                Foreground = System.Windows.Media.Brushes.White,
                BorderThickness = new System.Windows.Thickness(0)
            };

            var skipBtn = new System.Windows.Controls.Button
            {
                Content = "Пропустить",
                IsCancel = true,
                Height = 36,
                Margin = new System.Windows.Thickness(0, 6, 0, 0),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch,
                Cursor = System.Windows.Input.Cursors.Hand,
                FontSize = 14,
                Background = System.Windows.Media.Brushes.Transparent,
                Foreground = dimText,
                BorderThickness = new System.Windows.Thickness(0)
            };
            skipBtn.MouseEnter += (_, _) => skipBtn.Foreground = textBrush;
            skipBtn.MouseLeave += (_, _) => skipBtn.Foreground = dimText;

            // --- Layout ---
            var fieldPanel = new System.Windows.Controls.StackPanel { Margin = new System.Windows.Thickness(24, 0, 24, 24) };
            fieldPanel.Children.Add(titleText);
            fieldPanel.Children.Add(box);
            fieldPanel.Children.Add(okBtn);
            fieldPanel.Children.Add(skipBtn);

            var outerStack = new System.Windows.Controls.StackPanel();
            outerStack.Children.Add(headerBar);
            outerStack.Children.Add(fieldPanel);

            var win = new System.Windows.Window
            {
                Title = "Steam Guard",
                Content = outerStack,
                Width = 360,
                SizeToContent = System.Windows.SizeToContent.Height,
                ResizeMode = System.Windows.ResizeMode.NoResize,
                WindowStartupLocation = System.Windows.WindowStartupLocation.CenterOwner,
                Owner = owner,
                FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
                Topmost = true,
                Background = innerBg,
                Foreground = textBrush,
                WindowStyle = System.Windows.WindowStyle.None,
                AllowsTransparency = true,
                BorderThickness = new System.Windows.Thickness(0)
            };

            outerStack.MouseLeftButtonDown += (_, _) => win.DragMove();

            string? result = null;
            okBtn.Click += (_, _) =>
            {
                result = box.Text.Trim();
                win.DialogResult = true;
                win.Close();
            };

            return win.ShowDialog() == true ? result : null;
        }

        private void OnDownloadProgress(int percent)
        {
            Dispatcher.Invoke(() =>
            {
                if (!_downloadNotified && percent > 0)
                {
                    _downloadNotified = true;
                    NotificationService.Show("Скачивание DayZ сервера началось", 2500);
                }

                DownloadProgressBar.Value = percent;
                ProgressPercentText.Text = $"{percent}%";
                if (percent >= 100)
                    ProgressPanel.Visibility = Visibility.Collapsed;
            });
        }

        private void OnSteamOutput(LogEntry entry)
        {
            Dispatcher.Invoke(() => _serverRunner.Log.Add(entry));
        }

        // --- RCon tab ---

        private readonly RConService _rcon = new();

        private void RConConnect_Click(object sender, RoutedEventArgs e)
        {
            var host = RConHostBox.Text.Trim();
            if (!int.TryParse(RConPortBox.Text, out var port))
            {
                NotificationService.Show("Некорректный порт", 2000);
                return;
            }

            // Use password from config if set, otherwise from saved field
            var password = RConPassBox.Text;
            if (string.IsNullOrWhiteSpace(password))
            {
                // Try from current config
                password = CollectConfigFromUI().RConPassword;
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                NotificationService.Show("Укажите RCon пароль в настройках сети", 2000);
                return;
            }

            RConLogBox.ItemsSource = _rcon.Log;
            _rcon.Log.CollectionChanged -= _onRconLogChanged;
            _rcon.Log.CollectionChanged += _onRconLogChanged;

            _rcon.Connect(host, port, password);

            if (_rcon.IsConnected)
            {
                RConConnectBtn.Visibility = Visibility.Collapsed;
                RConDisconnectBtn.Visibility = Visibility.Visible;
            }
        }

        private void RConDisconnect_Click(object sender, RoutedEventArgs e)
        {
            _rcon.Disconnect();
            RConConnectBtn.Visibility = Visibility.Visible;
            RConDisconnectBtn.Visibility = Visibility.Collapsed;
        }

        private void RConSend_Click(object sender, RoutedEventArgs e)
        {
            SendRConCommand();
        }

        private void RConCmdBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                SendRConCommand();
                e.Handled = true;
            }
        }

        private void SendRConCommand()
        {
            var cmd = RConCmdBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(cmd)) return;
            _rcon.SendCommand(cmd);
            RConCmdBox.Clear();
        }

        // --- Stats tab ---

        private readonly ServerStatsService _statsService = new();

        private void RefreshStats_Click(object sender, RoutedEventArgs e)
        {
            var profilesDir = Path.Combine(ServerPathBox.Text, "profiles");
            if (!Directory.Exists(profilesDir))
            {
                MessageBox.Show("Папка profiles не найдена.", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var stats = _statsService.GetPlayerStats(profilesDir);
            StatsListBox.ItemsSource = stats;

            var online = stats.Count(s => s.IsOnline);
            var total = stats.Count;
            StatsSummary.Text = $"Онлайн: {online} | Всего игроков: {total}";
        }

    }
}
