using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Media;

namespace RGC.Services
{
    public class ServerRunner : IDisposable
    {
        private Process? _process;

        public ObservableCollection<LogEntry> Log { get; } = new();
        public bool IsRunning => _process?.HasExited == false;

        public void Start(string serverDir, string configFile, int port, int cpuCores,
                          string batchName, string modList, string serverModList)
        {
            if (IsRunning) return;

            var exe = Path.Combine(serverDir, "DayZServer_x64.exe");
            if (!File.Exists(exe))
            {
                Log.Add(new LogEntry($"Ошибка: DayZServer_x64.exe не найден в {serverDir}", Colors.Red));
                return;
            }

            var args = $"-config={configFile} -port={port} -cpuCount={cpuCores} " +
                       $"-dologs -adminlog -netlog -freezecheck " +
                       $"\"-BEpath={serverDir}\\battleye\" \"-profiles={serverDir}\\profiles\"";

            if (!string.IsNullOrWhiteSpace(modList))
                args += $" \"-mod={modList}\"";
            if (!string.IsNullOrWhiteSpace(serverModList))
                args += $" \"-servermod={serverModList}\"";

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = exe,
                    Arguments = args,
                    WorkingDirectory = serverDir,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                _process = new Process { StartInfo = psi, EnableRaisingEvents = true };
                _process.OutputDataReceived += OnOutput;
                _process.ErrorDataReceived += OnOutput;
                _process.Exited += (_, _) =>
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        Log.Add(new LogEntry("Процесс сервера завершён", Colors.Gray));
                    });
                };

                _process.Start();
                _process.BeginOutputReadLine();
                _process.BeginErrorReadLine();

                Log.Add(new LogEntry($"Сервер запущен (PID: {_process.Id})", Colors.LimeGreen));
                Log.Add(new LogEntry($"> {exe} {args}", Colors.DimGray));
            }
            catch (Exception ex)
            {
                Log.Add(new LogEntry($"Ошибка запуска: {ex.Message}", Colors.Red));
            }
        }

        public void Stop()
        {
            if (_process?.HasExited == false)
            {
                _process.Kill(entireProcessTree: true);
                Log.Add(new LogEntry("Сервер остановлен", Colors.Orange));
            }
        }

        private void OnOutput(object sender, DataReceivedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(e.Data)) return;
            var line = e.Data;
            System.Windows.Media.Color color;

            if (line.Contains("Error", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("ERROR", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("FATAL", StringComparison.OrdinalIgnoreCase))
                color = Colors.Red;
            else if (line.Contains("Warning", StringComparison.OrdinalIgnoreCase) ||
                     line.Contains("WARNING", StringComparison.OrdinalIgnoreCase))
                color = Colors.Orange;
            else if (line.Contains("connected", StringComparison.OrdinalIgnoreCase) ||
                     line.Contains("disconnected", StringComparison.OrdinalIgnoreCase) ||
                     line.Contains("Mission", StringComparison.OrdinalIgnoreCase) ||
                     line.Contains("loaded", StringComparison.OrdinalIgnoreCase) ||
                     line.Contains("started", StringComparison.OrdinalIgnoreCase))
                color = Colors.DodgerBlue;
            else
                color = Colors.Silver;

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                Log.Add(new LogEntry(line, color));
            });
        }

        public void Dispose()
        {
            Stop();
            _process?.Dispose();
        }
    }

    public class LogEntry
    {
        public string Text { get; }
        public SolidColorBrush Brush { get; }

        public LogEntry(string text, System.Windows.Media.Color color)
        {
            Text = text;
            Brush = new SolidColorBrush(color);
            Brush.Freeze();
        }
    }
}
