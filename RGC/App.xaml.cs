using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using RGC.Models;
using RGC.Services;

namespace RGC
{
    public partial class App : System.Windows.Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            // Handle version update via command line
            if (e.Args.Length > 0 && e.Args[0] == "--update-version" && e.Args.Length >= 2)
            {
                var ver = e.Args[1];
                var url = e.Args.Length > 2 ? e.Args[2] : "";
                var notes = e.Args.Length > 3 ? e.Args[3] : "";
                var ok = FirebaseService.SetUpdateVersionAsync(ver, url, notes).GetAwaiter().GetResult();
                Console.WriteLine(ok ? "OK" : "FAIL");
                Environment.Exit(ok ? 0 : 1);
                return;
            }

            DispatcherUnhandledException += (_, args) =>
            {
                System.Windows.MessageBox.Show(
                    $"Ошибка:\n{args.Exception.Message}\n\n{args.Exception.StackTrace}",
                    "RGC — Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                args.Handled = true;
            };

            AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            {
                var ex = args.ExceptionObject as Exception;
                System.Windows.MessageBox.Show(
                    $"Критическая ошибка:\n{ex?.Message}\n\n{ex?.StackTrace}",
                    "RGC — Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            };

            ApplyTheme(SettingsService.Theme);

            if (!ActivationService.IsActivated)
            {
                var result = ShowActivationDialog();
                if (!result) return;
            }

            _ = CheckForUpdatesAsync();

            System.Windows.Application.Current.Dispatcher.BeginInvoke(
                System.Windows.Threading.DispatcherPriority.Background,
                new Action(() => ShowStartupChoice()));
        }

        private static void ShowStartupChoice()
        {
            var c = System.Windows.Media.Color.FromRgb;
            var win = new Window
            {
                Width = 520,
                SizeToContent = System.Windows.SizeToContent.Height,
                ResizeMode = System.Windows.ResizeMode.NoResize,
                WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen,
                WindowStyle = System.Windows.WindowStyle.None,
                AllowsTransparency = true,
                Background = System.Windows.Media.Brushes.Transparent,
                FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
                Topmost = true
            };

            var headerBar = new System.Windows.Controls.Border
            {
                Height = 4,
                Background = new System.Windows.Media.SolidColorBrush(c(0x89, 0xb4, 0xfa)),
                CornerRadius = new System.Windows.CornerRadius(16, 16, 0, 0)
            };

            var titleBlock = new System.Windows.Controls.TextBlock
            {
                Text = RGC.Services.Localization.T("startup.ask"),
                FontSize = 18,
                FontWeight = System.Windows.FontWeights.Bold,
                Foreground = new System.Windows.Media.SolidColorBrush(c(0xcd, 0xd6, 0xf4)),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                Margin = new System.Windows.Thickness(0, 20, 0, 16)
            };

            var panel = new System.Windows.Controls.StackPanel { Margin = new System.Windows.Thickness(28, 0, 28, 20) };
            panel.Children.Add(titleBlock);
            panel.Children.Add(MakeChoiceBtn(win, "🖥", "startup.edit_server", "startup.edit_server_desc", () => { new ProjectLibraryWindow().Show(); win.Close(); }));
            panel.Children.Add(MakeChoiceBtn(win, "🔗", "startup.connect_ftp", "startup.connect_ftp_desc", () => { new ProjectLibraryWindow().Show(); win.Close(); }));
            panel.Children.Add(MakeChoiceBtn(win, "⚙", "startup.autobuild", "startup.autobuild_desc", () => { System.Windows.MessageBox.Show(RGC.Services.Localization.T("startup.autobuild_soon")); }));

            var outer = new System.Windows.Controls.StackPanel();
            outer.Children.Add(headerBar);
            outer.Children.Add(panel);

            win.Content = new System.Windows.Controls.Border
            {
                Background = new System.Windows.Media.SolidColorBrush(c(0x18, 0x18, 0x25)),
                BorderBrush = new System.Windows.Media.SolidColorBrush(c(0x31, 0x32, 0x44)),
                BorderThickness = new System.Windows.Thickness(1),
                CornerRadius = new System.Windows.CornerRadius(16),
                ClipToBounds = true,
                Child = outer
            };

            win.ShowDialog();
        }

        private static System.Windows.Controls.Button MakeChoiceBtn(Window parent, string icon, string labelKey, string descKey, Action onClick)
        {
            var label = RGC.Services.Localization.T(labelKey);
            var desc = RGC.Services.Localization.T(descKey);
            var c = System.Windows.Media.Color.FromRgb;

            var iconBlock = new System.Windows.Controls.TextBlock
            {
                Text = icon,
                FontSize = 22,
                VerticalAlignment = System.Windows.VerticalAlignment.Center,
                Margin = new System.Windows.Thickness(0, 0, 12, 0)
            };

            var labelBlock = new System.Windows.Controls.TextBlock
            {
                Text = label,
                FontSize = 14,
                FontWeight = System.Windows.FontWeights.SemiBold,
                Foreground = new System.Windows.Media.SolidColorBrush(c(0xcd, 0xd6, 0xf4))
            };

            var descBlock = new System.Windows.Controls.TextBlock
            {
                Text = desc,
                FontSize = 11,
                Foreground = new System.Windows.Media.SolidColorBrush(c(0x6c, 0x70, 0x8b)),
                Margin = new System.Windows.Thickness(0, 1, 0, 0)
            };

            var stack = new System.Windows.Controls.StackPanel();
            stack.Children.Add(labelBlock);
            stack.Children.Add(descBlock);

            var innerGrid = new System.Windows.Controls.Grid();
            innerGrid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = System.Windows.GridLength.Auto });
            innerGrid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new System.Windows.GridLength(1, System.Windows.GridUnitType.Star) });
            System.Windows.Controls.Grid.SetColumn(iconBlock, 0);
            System.Windows.Controls.Grid.SetColumn(stack, 1);
            innerGrid.Children.Add(iconBlock);
            innerGrid.Children.Add(stack);

            var btn = new System.Windows.Controls.Button
            {
                Content = innerGrid,
                Height = 62,
                Margin = new System.Windows.Thickness(0, 0, 0, 8),
                Background = new System.Windows.Media.SolidColorBrush(c(0x1e, 0x1e, 0x2e)),
                Foreground = new System.Windows.Media.SolidColorBrush(c(0xcd, 0xd6, 0xf4)),
                BorderThickness = new System.Windows.Thickness(1),
                BorderBrush = new System.Windows.Media.SolidColorBrush(c(0x31, 0x32, 0x44)),
                Cursor = System.Windows.Input.Cursors.Hand,
                HorizontalContentAlignment = System.Windows.HorizontalAlignment.Stretch,
                Padding = new System.Windows.Thickness(14, 0, 14, 0),
                FontSize = 12
            };
            btn.Click += (_, _) => onClick();
            return btn;
        }

        private static bool ShowActivationDialog()
        {
            var c = System.Windows.Media.Color.FromRgb;
            var win = new Window
            {
                Title = "Активация RGC",
                Width = 400,
                SizeToContent = System.Windows.SizeToContent.Height,
                ResizeMode = System.Windows.ResizeMode.NoResize,
                WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen,
                WindowStyle = System.Windows.WindowStyle.None,
                AllowsTransparency = true,
                Background = System.Windows.Media.Brushes.Transparent,
                FontFamily = new System.Windows.Media.FontFamily("Segoe UI")
            };

            var keyBox = new System.Windows.Controls.TextBox
            {
                FontSize = 18,
                Padding = new System.Windows.Thickness(14, 12, 14, 12),
                Background = new System.Windows.Media.SolidColorBrush(c(0x31, 0x32, 0x44)),
                Foreground = new System.Windows.Media.SolidColorBrush(c(0xcd, 0xd6, 0xf4)),
                BorderThickness = new System.Windows.Thickness(0),
                CaretBrush = new System.Windows.Media.SolidColorBrush(c(0x89, 0xb4, 0xfa)),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch,
                HorizontalContentAlignment = System.Windows.HorizontalAlignment.Center,
                TextAlignment = System.Windows.TextAlignment.Center
            };

            var statusText = new System.Windows.Controls.TextBlock
            {
                FontSize = 12,
                Foreground = new System.Windows.Media.SolidColorBrush(c(0x6c, 0x70, 0x8b)),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                Margin = new System.Windows.Thickness(0, 8, 0, 0)
            };

            var activateBtn = new System.Windows.Controls.Button
            {
                Content = "Активировать",
                IsDefault = true,
                Height = 38,
                Background = new System.Windows.Media.SolidColorBrush(c(0x89, 0xb4, 0xfa)),
                Foreground = new System.Windows.Media.SolidColorBrush(c(0x1e, 0x1e, 0x2e)),
                BorderThickness = new System.Windows.Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand,
                FontSize = 14,
                FontWeight = System.Windows.FontWeights.Bold,
                Margin = new System.Windows.Thickness(0, 12, 0, 0)
            };

            var exitBtn = new System.Windows.Controls.Button
            {
                Content = "Выйти",
                IsCancel = true,
                Height = 32,
                Background = System.Windows.Media.Brushes.Transparent,
                Foreground = new System.Windows.Media.SolidColorBrush(c(0x6c, 0x70, 0x8b)),
                BorderThickness = new System.Windows.Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand,
                FontSize = 12,
                Margin = new System.Windows.Thickness(0, 4, 0, 0)
            };
            exitBtn.Click += (_, _) => { win.DialogResult = false; win.Close(); };

            activateBtn.Click += async (_, _) =>
            {
                var key = keyBox.Text.Trim();
                if (string.IsNullOrEmpty(key))
                {
                    statusText.Text = "Введите ключ активации";
                    statusText.Foreground = new System.Windows.Media.SolidColorBrush(c(0xf3, 0x8b, 0xa8));
                    return;
                }

                activateBtn.IsEnabled = false;
                activateBtn.Content = "⏳ Проверка...";
                statusText.Text = "Проверка ключа...";
                statusText.Foreground = new SolidColorBrush(c(0x6c, 0x70, 0x8b));

                var error = await Task.Run(() => ActivationService.ActivateAsync(key));

                if (error == null)
                {
                    statusText.Text = "✅ Активация успешна!";
                    statusText.Foreground = new SolidColorBrush(c(0xa6, 0xe3, 0xa1));
                    await Task.Delay(800);
                    win.DialogResult = true;
                    win.Close();
                }
                else
                {
                    statusText.Text = error;
                    statusText.Foreground = new System.Windows.Media.SolidColorBrush(c(0xf3, 0x8b, 0xa8));
                    activateBtn.IsEnabled = true;
                    activateBtn.Content = "Активировать";
                }
            };

            var headerBar = new System.Windows.Controls.Border
            {
                Height = 4,
                Background = new SolidColorBrush(c(0x89, 0xb4, 0xfa)),
                BorderThickness = new Thickness(0)
            };

            var fieldPanel = new System.Windows.Controls.StackPanel
            { Margin = new Thickness(28, 24, 28, 24) };
            fieldPanel.Children.Add(new System.Windows.Controls.TextBlock
            {
                Text = "Активация RGC",
                FontSize = 20,
                FontWeight = System.Windows.FontWeights.Bold,
                Foreground = new System.Windows.Media.SolidColorBrush(c(0xcd, 0xd6, 0xf4)),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                Margin = new System.Windows.Thickness(0, 0, 0, 4)
            });
            fieldPanel.Children.Add(new System.Windows.Controls.TextBlock
            {
                Text = "Введите ключ активации",
                FontSize = 12,
                Foreground = new System.Windows.Media.SolidColorBrush(c(0x6c, 0x70, 0x8b)),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                Margin = new System.Windows.Thickness(0, 0, 0, 16)
            });
            fieldPanel.Children.Add(keyBox);
            fieldPanel.Children.Add(statusText);
            fieldPanel.Children.Add(activateBtn);
            fieldPanel.Children.Add(exitBtn);

            var outerStack = new System.Windows.Controls.StackPanel();
            outerStack.Children.Add(headerBar);
            outerStack.Children.Add(fieldPanel);

            win.Content = outerStack;
            win.Background = new SolidColorBrush(c(0x18, 0x18, 0x25));
            win.Topmost = true;

            return win.ShowDialog() == true;
        }

        public void ApplyTheme(string theme)
        {
            var dict = new ResourceDictionary();
            if (theme == "Light")
                dict.Source = new Uri("Themes/LightTheme.xaml", UriKind.Relative);
            else
                dict.Source = new Uri("Themes/DarkTheme.xaml", UriKind.Relative);

            Resources.MergedDictionaries.Clear();
            Resources.MergedDictionaries.Add(dict);
        }

        private static async Task CheckForUpdatesAsync()
        {
            try
            {
                var current = "v1.1.0";

                var (version, downloadUrl, notes) = await FirebaseService.CheckUpdateAsync();
                if (version != null && version != current)
                    ShowUpdateDialog(version, downloadUrl, notes);

                var announcements = await FirebaseService.GetAnnouncementsAsync(current);
                if (announcements.Count > 0)
                    ShowAnnouncements(announcements);
            }
            catch { }
        }

        private static void ShowAnnouncements(List<Announcement> announcements)
        {
            var c = System.Windows.Media.Color.FromRgb;
            foreach (var ann in announcements)
            {
                var text = SettingsService.Language == "EN" && !string.IsNullOrEmpty(ann.TextEn)
                    ? ann.TextEn : ann.Text;

                if (ann.Type == "critical")
                {
                    System.Windows.MessageBox.Show(text, "Announcement", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                else
                {
                    NotificationService.Show(text, ann.Type == "warning" ? 6000 : 4000);
                }
            }
        }

        private static void ShowUpdateDialog(string? version, string? downloadUrl, string? notes)
        {
            var c = System.Windows.Media.Color.FromRgb;
            var win = new Window
            {
                Title = "Обновление",
                Width = 420,
                SizeToContent = System.Windows.SizeToContent.Height,
                ResizeMode = System.Windows.ResizeMode.NoResize,
                WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen,
                WindowStyle = System.Windows.WindowStyle.None,
                AllowsTransparency = true,
                Background = System.Windows.Media.Brushes.Transparent,
                FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
                Topmost = true
            };

            var headerBar = new System.Windows.Controls.Border
            {
                Height = 4,
                Background = new System.Windows.Media.SolidColorBrush(c(0x89, 0xb4, 0xfa)),
                BorderThickness = new System.Windows.Thickness(0),
                CornerRadius = new System.Windows.CornerRadius(16, 16, 0, 0)
            };

            var iconCircle = new System.Windows.Controls.Border
            {
                Width = 56, Height = 56,
                CornerRadius = new System.Windows.CornerRadius(28),
                Background = new System.Windows.Media.SolidColorBrush(c(0x31, 0x32, 0x44)),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                Margin = new System.Windows.Thickness(0, 20, 0, 0),
                Child = new System.Windows.Controls.TextBlock
                {
                    Text = "🔄",
                    FontSize = 24,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    VerticalAlignment = System.Windows.VerticalAlignment.Center
                }
            };

            var titleText = new System.Windows.Controls.TextBlock
            {
                Text = $"Доступно обновление {version}",
                FontSize = 18,
                FontWeight = System.Windows.FontWeights.Bold,
                Foreground = new System.Windows.Media.SolidColorBrush(c(0xcd, 0xd6, 0xf4)),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                Margin = new System.Windows.Thickness(0, 12, 0, 4)
            };

            var notesBlock = new System.Windows.Controls.TextBlock
            {
                Text = notes ?? "",
                FontSize = 12,
                Foreground = new System.Windows.Media.SolidColorBrush(c(0x6c, 0x70, 0x8b)),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                TextAlignment = System.Windows.TextAlignment.Center,
                Margin = new System.Windows.Thickness(28, 4, 28, 0),
                TextWrapping = System.Windows.TextWrapping.Wrap
            };

            var updateBtn = new System.Windows.Controls.Button
            {
                Content = "⬇ Скачать обновление",
                Height = 38,
                Margin = new System.Windows.Thickness(28, 16, 28, 0),
                Background = new System.Windows.Media.SolidColorBrush(c(0x89, 0xb4, 0xfa)),
                Foreground = new System.Windows.Media.SolidColorBrush(c(0x1e, 0x1e, 0x2e)),
                BorderThickness = new System.Windows.Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand,
                FontSize = 14,
                FontWeight = System.Windows.FontWeights.Bold
            };
            updateBtn.Click += (_, _) =>
            {
                if (!string.IsNullOrEmpty(downloadUrl))
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    { FileName = downloadUrl, UseShellExecute = true });
                win.Close();
            };

            var laterBtn = new System.Windows.Controls.Button
            {
                Content = "Позже",
                Height = 32,
                Margin = new System.Windows.Thickness(28, 6, 28, 20),
                Background = System.Windows.Media.Brushes.Transparent,
                Foreground = new System.Windows.Media.SolidColorBrush(c(0x6c, 0x70, 0x8b)),
                BorderThickness = new System.Windows.Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand,
                FontSize = 12,
                IsCancel = true
            };
            laterBtn.Click += (_, _) => win.Close();

            var panel = new System.Windows.Controls.StackPanel();
            panel.Children.Add(iconCircle);
            panel.Children.Add(titleText);
            panel.Children.Add(notesBlock);
            panel.Children.Add(updateBtn);
            panel.Children.Add(laterBtn);

            var outer = new System.Windows.Controls.StackPanel();
            outer.Children.Add(headerBar);
            outer.Children.Add(panel);

            // Рамка вокруг окна
            var borderBrush = new System.Windows.Media.SolidColorBrush(c(0x31, 0x32, 0x44));
            var rootBorder = new System.Windows.Controls.Border
            {
                Background = new System.Windows.Media.SolidColorBrush(c(0x18, 0x18, 0x25)),
                BorderBrush = borderBrush,
                BorderThickness = new System.Windows.Thickness(1),
                CornerRadius = new System.Windows.CornerRadius(16),
                ClipToBounds = true,
                Child = outer
            };

            win.Content = rootBorder;
            win.Background = System.Windows.Media.Brushes.Transparent;
            win.ShowDialog();
        }
    }
}
