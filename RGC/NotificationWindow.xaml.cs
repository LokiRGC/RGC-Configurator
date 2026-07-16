using System.Windows;
using System.Windows.Media.Animation;

namespace RGC
{
    public partial class NotificationWindow : Window
    {
        private readonly int _durationMs;
        private double _targetTop;

        public NotificationWindow(string message, int durationMs = 3000)
        {
            InitializeComponent();
            MessageText.Text = message;
            _durationMs = durationMs;

            Left = SystemParameters.WorkArea.Right - Width - 16;
            Top = SystemParameters.WorkArea.Bottom + 20;

            Loaded += (_, _) =>
            {
                SlideTo(_targetTop);
                StartCloseTimer();
            };
        }

        public void SlideTo(double targetTop)
        {
            _targetTop = targetTop;
            if (!IsLoaded) return;

            BeginAnimation(TopProperty, null);
            var anim = new DoubleAnimation
            {
                To = targetTop,
                Duration = new Duration(TimeSpan.FromMilliseconds(400)),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            BeginAnimation(TopProperty, anim);
        }

        private void StartCloseTimer()
        {
            var timer = new System.Timers.Timer(_durationMs) { AutoReset = false };
            timer.Elapsed += (_, _) => Dispatcher.Invoke(() =>
            {
                BeginAnimation(TopProperty, null);
                var anim = new DoubleAnimation
                {
                    To = SystemParameters.WorkArea.Bottom + 20,
                    Duration = new Duration(TimeSpan.FromMilliseconds(400)),
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
                };
                anim.Completed += (_, _) => Close();
                BeginAnimation(TopProperty, anim);
            });
            timer.Start();
        }
    }
}
