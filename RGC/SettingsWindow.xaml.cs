using System.Windows;
using System.Windows.Input;
using RGC.Services;

namespace RGC
{
    public partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            InitializeComponent();
            LoadSettings();
            ApplyLang();
        }

        private void LoadSettings()
        {
            AutoStartCheck.IsChecked = SettingsService.AutoStart;
            DarkThemeRadio.IsChecked = SettingsService.Theme == "Dark";
            LightThemeRadio.IsChecked = SettingsService.Theme == "Light";
            ConfirmExitCheck.IsChecked = SettingsService.ConfirmExit;
            MinimizeTrayCheck.IsChecked = SettingsService.MinimizeToTray;
            LangRu.IsChecked = SettingsService.Language == "RU";
            LangEn.IsChecked = SettingsService.Language != "RU";
            NotifSoundCheck.IsChecked = SettingsService.NotificationSound;
            var durSec = Math.Max(1, Math.Min(10, SettingsService.NotificationDuration / 1000));
            DurationSlider.Value = durSec;
            DurationLabel.Text = $"{durSec} {RGC.Services.Localization.T("settings.sec")}";
        }

        private void ApplyLang()
        {
            WinTitle.Text = RGC.Services.Localization.T("settings.title");
            SectGeneral.Text = RGC.Services.Localization.T("settings.general");
            ConfirmExitCheck.Content = RGC.Services.Localization.T("settings.confirm_exit");
            MinimizeTrayCheck.Content = RGC.Services.Localization.T("settings.minimize_tray");
            SectLang.Text = RGC.Services.Localization.T("settings.language");
            SectNotif.Text = RGC.Services.Localization.T("settings.notifications");
            NotifSoundCheck.Content = RGC.Services.Localization.T("settings.notif_sound");
            NotifDurationLabel.Text = RGC.Services.Localization.T("settings.notif_duration");
            SectAppearance.Text = RGC.Services.Localization.T("settings.appearance");
            AutoStartCheck.Content = RGC.Services.Localization.T("settings.autostart");
            DarkThemeRadio.Content = RGC.Services.Localization.T("settings.dark");
            LightThemeRadio.Content = RGC.Services.Localization.T("settings.light");
            CloseBtn.Content = RGC.Services.Localization.T("settings.close");
        }

        private void Window_Activated(object sender, EventArgs e) { }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                DragMove();
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();

        private void AutoStart_Changed(object sender, RoutedEventArgs e)
            => SettingsService.AutoStart = AutoStartCheck.IsChecked == true;

        private void Theme_Changed(object sender, RoutedEventArgs e)
        {
            var theme = LightThemeRadio.IsChecked == true ? "Light" : "Dark";
            SettingsService.Theme = theme;
            if (System.Windows.Application.Current is App app)
                app.ApplyTheme(theme);
        }

        private void ConfirmExit_Changed(object sender, RoutedEventArgs e)
            => SettingsService.ConfirmExit = ConfirmExitCheck.IsChecked == true;

        private void MinimizeTray_Changed(object sender, RoutedEventArgs e)
            => SettingsService.MinimizeToTray = MinimizeTrayCheck.IsChecked == true;

        private void Lang_Changed(object sender, RoutedEventArgs e)
        {
            if (LangRu.IsChecked == true)
                SettingsService.Language = "RU";
            else if (LangEn.IsChecked == true)
                SettingsService.Language = "EN";
            ApplyLang();
        }

        private void NotifSound_Changed(object sender, RoutedEventArgs e)
            => SettingsService.NotificationSound = NotifSoundCheck.IsChecked == true;

        private void Duration_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (DurationLabel == null) return;
            var sec = (int)DurationSlider.Value;
            DurationLabel.Text = $"{sec} {RGC.Services.Localization.T("settings.sec")}";
            SettingsService.NotificationDuration = sec * 1000;
        }
    }
}
