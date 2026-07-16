using System.Windows;
using System.Windows.Input;
using RGC.Services;
using MessageBox = System.Windows.MessageBox;

namespace RGC
{
    public partial class LoginWindow : Window
    {
        private readonly AuthService _auth = new();

        public LoginWindow()
        {
            InitializeComponent();
            LoadRemembered();
        }

        private void LoadRemembered()
        {
            var remembered = _auth.GetRemembered();
            if (remembered != null)
            {
                UsernameBox.Text = remembered;
                RememberCheckbox.IsChecked = true;
                PasswordBox.Focus();
            }
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                DragMove();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Login_Click(object sender, RoutedEventArgs e)
        {
            var result = _auth.Login(UsernameBox.Text, PasswordBox.Password);

            if (!result.Success)
                MessageBox.Show(result.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);

            StatusText.Text = result.Message;
            StatusText.Foreground = result.Success
                ? System.Windows.Media.Brushes.Green
                : System.Windows.Media.Brushes.Red;

            if (result.Success)
            {
                if (RememberCheckbox.IsChecked == true)
                    _auth.SaveRemembered(UsernameBox.Text);
                else
                    _auth.ClearRemembered();
                OpenMainWindow();
            }
        }

        private void Register_Click(object sender, RoutedEventArgs e)
        {
            var result = _auth.Register(UsernameBox.Text, PasswordBox.Password);

            if (!result.Success)
                MessageBox.Show(result.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);

            StatusText.Text = result.Message;
            StatusText.Foreground = result.Success
                ? System.Windows.Media.Brushes.Green
                : System.Windows.Media.Brushes.Red;

            if (result.Success)
            {
                if (RememberCheckbox.IsChecked == true)
                    _auth.SaveRemembered(UsernameBox.Text);
                else
                    _auth.ClearRemembered();
                OpenMainWindow();
            }
        }

        private void OpenMainWindow()
        {
            var main = new MainWindow();
            main.Show();
            Close();
        }
    }
}
