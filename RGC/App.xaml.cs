using System;
using System.Windows;
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
            var lib = new ProjectLibraryWindow();
            lib.Show();
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
