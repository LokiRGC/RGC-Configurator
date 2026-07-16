using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace RGC.Services
{
    public static class NotificationService
    {
        private static readonly List<NotificationWindow> _active = new();
        private static readonly object _lock = new();

        public static void Show(string message)
        {
            Show(message, SettingsService.NotificationDuration);
        }

        public static void Show(string message, int durationMs)
        {
            lock (_lock)
            {
                var win = new NotificationWindow(message, durationMs);
                win.Closed += (_, _) =>
                {
                    lock (_lock) _active.Remove(win);
                    RepositionAll();
                };
                _active.Add(win);
                win.Show();
                RepositionAll();
            }
        }

        private static void RepositionAll()
        {
            lock (_lock)
            {
                var bottom = SystemParameters.WorkArea.Bottom - 16;
                foreach (var w in _active.OrderBy(w => w.Top))
                {
                    w.SlideTo(bottom - w.Height);
                    bottom -= w.Height + 8;
                }
            }
        }
    }
}
