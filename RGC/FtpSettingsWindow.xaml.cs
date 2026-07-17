using System;
using System.Windows;
using RGC.Models;
using RGC.Services;
using MessageBox = System.Windows.MessageBox;
using Localization = RGC.Services.Localization;

namespace RGC
{
    public partial class FtpSettingsWindow : Window
    {
        private readonly FtpProject? _project;

        public FtpSettingsWindow(FtpProject? existing = null)
        {
            InitializeComponent();
            _project = existing;

            ApplyLang();
            if (_project != null)
                LoadProject();
            else
                DeleteBtn.Visibility = Visibility.Collapsed;
        }

        private void ApplyLang()
        {
            if (_project == null)
            {
                WinTitle.Text = Localization.T("ftp.title_new");
                SaveBtn.Content = Localization.T("ftp.create");
            }
            else
            {
                WinTitle.Text = Localization.T("ftp.title_edit");
                SaveBtn.Content = Localization.T("ftp.save");
            }
            CancelBtn.Content = Localization.T("ftp.cancel");
            DeleteBtn.Content = Localization.T("ftp.delete");

            NameLabel.Text = Localization.T("ftp.name");
            HostLabel.Text = Localization.T("ftp.host");
            PortLabel.Text = Localization.T("ftp.port");
            UserLabel.Text = Localization.T("ftp.username");
            PassLabel.Text = Localization.T("ftp.password");
            RemoteLabel.Text = Localization.T("ftp.connect_label");
            ConnectFtpBtn.Content = Localization.T("ftp.connect_btn");
            LocalLabel.Text = Localization.T("ftp.local_path");
        }

        private void LoadProject()
        {
            if (_project == null) return;
            NameBox.Text = _project.Name;
            HostBox.Text = _project.Host;
            PortBox.Text = _project.Port.ToString();
            UserBox.Text = _project.Username;
            PassBox.Password = _project.Password;
            LocalBox.Text = _project.LocalPath;
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            var name = NameBox.Text.Trim();
            var host = HostBox.Text.Trim();
            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(host))
            {
                MessageBox.Show(this, Localization.T("ftp.fill_required"), "",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!int.TryParse(PortBox.Text.Trim(), out var port) || port < 1 || port > 65535)
                port = 21;

            if (_project != null)
            {
                _project.Name = name;
                _project.Host = host;
                _project.Port = port;
                _project.Username = UserBox.Text.Trim();
                _project.Password = PassBox.Password;
                _project.LocalPath = LocalBox.Text.Trim();
                _project.LastOpenedAt = DateTime.Now;
                FtpProjectService.Update(_project);
            }
            else
            {
                var ftp = new FtpProject
                {
                    Name = name,
                    Host = host,
                    Port = port,
                    Username = UserBox.Text.Trim(),
                    Password = PassBox.Password,
                    RemotePath = "/",
                    LocalPath = LocalBox.Text.Trim()
                };
                FtpProjectService.Create(ftp);
            }

            DialogResult = true;
            Close();
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            if (_project == null) return;
            var msg = string.Format(Localization.T("ftp.delete_confirm"), _project.Name);
            if (MessageBox.Show(this, msg, Localization.T("lib.delete_title"),
                MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                FtpProjectService.Delete(_project.Id);
                DialogResult = true;
                Close();
            }
        }

        private void BrowseLocal_Click(object sender, RoutedEventArgs e)
        {
            using var dlg = new System.Windows.Forms.FolderBrowserDialog();
            if (!string.IsNullOrEmpty(LocalBox.Text))
                dlg.SelectedPath = LocalBox.Text;
            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                LocalBox.Text = dlg.SelectedPath;
        }

        private void ConnectFtp_Click(object sender, RoutedEventArgs e)
        {
            var host = HostBox.Text.Trim();
            if (string.IsNullOrEmpty(host))
            {
                MessageBox.Show(this, Localization.T("ftp.fill_required"), "",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!int.TryParse(PortBox.Text.Trim(), out var port) || port < 1 || port > 65535)
                port = 21;

            // Save project first
            if (_project != null)
            {
                _project.Host = HostBox.Text.Trim();
                _project.Port = port;
                _project.Username = UserBox.Text.Trim();
                _project.Password = PassBox.Password;
                FtpProjectService.Update(_project);
            }

            // Close ProjectLibraryWindow (owner) and self
            if (Owner is ProjectLibraryWindow lib)
                lib.Close();

            var localPath = _project?.LocalPath ?? "";
            var remotePath = _project?.RemotePath ?? "/";
            var browser = new FtpBrowserWindow(host, port,
                UserBox.Text.Trim(), PassBox.Password, remotePath, localPath);
            browser.Show();
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
