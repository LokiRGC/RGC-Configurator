using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using RGC.Services;

namespace RGC
{
    public partial class App : System.Windows.Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
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

            var lib = new ProjectLibraryWindow();
            lib.Show();
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
    }
}
