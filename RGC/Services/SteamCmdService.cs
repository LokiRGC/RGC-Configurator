using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;

namespace RGC.Services
{
    public class SteamCmdService
    {
        private static readonly string SteamCmdDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RGC", "steamcmd");

        private static string? _systemSteamCmdPath;

        public static string ManagedSteamCmdExe => Path.Combine(SteamCmdDir, "steamcmd.exe");

        public static string? SystemSteamCmdExe
        {
            get
            {
                if (_systemSteamCmdPath == null)
                    _systemSteamCmdPath = FindSystemSteamCmd();
                return _systemSteamCmdPath;
            }
        }

        public static string SteamCmdExe => ManagedSteamCmdExe;

        public static bool IsSteamCmdInstalled => File.Exists(SteamCmdExe);
        public static bool IsSystemSteamCmd => SystemSteamCmdExe != null;

        public static void RefreshDetection() => _systemSteamCmdPath = FindSystemSteamCmd();

        private static string? FindSystemSteamCmd()
        {
            try
            {
                var psi = new ProcessStartInfo("where", "steamcmd")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true
                };
                using var proc = Process.Start(psi);
                var path = proc?.StandardOutput.ReadLine()?.Trim();
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                    return path;
            }
            catch { }

            var commonPaths = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam", "steamcmd", "steamcmd.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Steam", "steamcmd", "steamcmd.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam", "steamapps", "common", "SteamCMD", "steamcmd.exe"),
                @"C:\SteamCMD\steamcmd.exe",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Steam", "steamcmd", "steamcmd.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Steam", "steamcmd", "steamcmd.exe"),
            };

            return commonPaths.FirstOrDefault(File.Exists);
        }

        public event Action<LogEntry>? Output;
        public event Action<int>? DownloadProgress;

        public async void InstallServer(string installDir, string? steamLogin = null, string? steamPass = null,
            Func<Task<string?>>? getGuardCode = null, string logPrefix = "SteamCMD")
        {
            try
            {
                if (!File.Exists(ManagedSteamCmdExe))
                {
                    if (IsSystemSteamCmd)
                        Output?.Invoke(new LogEntry($"[{logPrefix}] Системный steamcmd найден ({SystemSteamCmdExe}), но используем управляемую копию для избежания проблем с правами", Colors.DodgerBlue));
                    Output?.Invoke(new LogEntry($"[{logPrefix}] Скачиваю steamcmd...", Colors.Gray));
                    await DownloadSteamCmd();
                    Output?.Invoke(new LogEntry($"[{logPrefix}] steamcmd установлен в {SteamCmdDir}", Colors.LimeGreen));
                }
                else
                {
                    Output?.Invoke(new LogEntry($"[{logPrefix}] steamcmd найден в папке приложения", Colors.DodgerBlue));
                }

                // Initialize steamcmd (self-update)
                Output?.Invoke(new LogEntry($"[{logPrefix}] Инициализация steamcmd...", Colors.Gray));
                await RunSteamCmd($"+quit", logPrefix);

                Output?.Invoke(new LogEntry($"[{logPrefix}] Установка/обновление DayZ Server...", Colors.Gray));
                Output?.Invoke(new LogEntry($"[{logPrefix}] Путь: {installDir}", Colors.DimGray));

                if (!Directory.Exists(installDir))
                {
                    Directory.CreateDirectory(installDir);
                    Output?.Invoke(new LogEntry($"[{logPrefix}] Создана папка: {installDir}", Colors.DimGray));
                }

                // Pre-flight: check write access
                try
                {
                    var testFile = Path.Combine(installDir, ".rgc_write_test");
                    File.WriteAllText(testFile, "test");
                    File.Delete(testFile);
                }
                catch (Exception ex)
                {
                    Output?.Invoke(new LogEntry($"[{logPrefix}] ❌ Нет прав на запись в папку: {ex.Message}", Colors.Red));
                    return;
                }
                
                // Check free disk space (need at least 5 GB)
                try
                {
                    var drive = new DriveInfo(Path.GetPathRoot(installDir)!);
                    if (drive.AvailableFreeSpace < 5L * 1024 * 1024 * 1024)
                        Output?.Invoke(new LogEntry($"[{logPrefix}] ⚠ Мало места на диске {drive.Name}: {drive.AvailableFreeSpace / 1024 / 1024 / 1024:F1} GB свободно", Colors.Orange));
                }
                catch { }

                var loginArg = string.IsNullOrEmpty(steamLogin)
                    ? "+login anonymous"
                    : $"+login \"{steamLogin}\" \"{steamPass}\"";
                var exitCode = await RunSteamCmd($"+force_install_dir \"{installDir}\" {loginArg} +app_update 223350 validate +quit", logPrefix, getGuardCode);

                // Read steamcmd logs for diagnostics
                if (exitCode != 0)
                {
                    var logDir = Path.Combine(SteamCmdDir, "logs");
                    if (Directory.Exists(logDir))
                    {
                        foreach (var logFile in Directory.GetFiles(logDir, "*.txt"))
                        {
                            try
                            {
                                var logContent = File.ReadAllText(logFile, Encoding.UTF8);
                                if (!string.IsNullOrWhiteSpace(logContent))
                                    Output?.Invoke(new LogEntry($"[{logPrefix}] 📄 {Path.GetFileName(logFile)}:\n{logContent.Trim()}", Colors.DimGray));
                            }
                            catch { }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Output?.Invoke(new LogEntry($"[{logPrefix}] {ex.Message}", Colors.Red));
            }
        }

        private async System.Threading.Tasks.Task<int> RunSteamCmd(string args, string logPrefix,
            Func<Task<string?>>? getGuardCode = null)
        {
            var logDir = Path.Combine(SteamCmdDir, "logs");
            Directory.CreateDirectory(logDir);
            var stderrLog = Path.Combine(logDir, "stderr.txt");
            var stdoutLog = Path.Combine(logDir, "stdout.txt");

            // Clean previous logs
            try { if (File.Exists(stderrLog)) File.Delete(stderrLog); } catch { }
            try { if (File.Exists(stdoutLog)) File.Delete(stdoutLog); } catch { }

            var psi = new ProcessStartInfo
            {
                FileName = ManagedSteamCmdExe,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
                CreateNoWindow = true
            };

            using var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
            var guardCodeSent = false;

            // Also capture stdout to a file for real-time monitoring
            var stdoutFile = File.CreateText(stdoutLog);
            stdoutFile.AutoFlush = true;

            proc.OutputDataReceived += (_, e) =>
            {
                if (e.Data != null)
                {
                    lock (stdoutFile) stdoutFile.WriteLine(e.Data);
                    guardCodeSent = ParseSteamOutput(e.Data, logPrefix, guardCodeSent, proc, getGuardCode) || guardCodeSent;
                }
            };
            proc.ErrorDataReceived += (_, e) =>
            {
                if (e.Data != null)
                    guardCodeSent = ParseSteamOutput(e.Data, logPrefix, guardCodeSent, proc, getGuardCode) || guardCodeSent;
            };
            proc.Start();

            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();

            // Background watcher: tail the stderr.txt file (steamcmd flushes it in real-time)
            var logTailer = Task.Run(async () =>
            {
                var logFile = stderrLog;
                long lastSize = 0;
                while (!proc.HasExited)
                {
                    try
                    {
                        if (File.Exists(logFile))
                        {
                            var fi = new FileInfo(logFile);
                            if (fi.Length > lastSize)
                            {
                                using var fs = new FileStream(logFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                                fs.Seek(lastSize, SeekOrigin.Begin);
                                using var reader = new StreamReader(fs, Encoding.UTF8);
                                string? line;
                                while ((line = reader.ReadLine()) != null)
                                {
                                    if (!string.IsNullOrWhiteSpace(line))
                                        guardCodeSent = ParseSteamOutput(line, logPrefix, guardCodeSent, proc, getGuardCode) || guardCodeSent;
                                }
                                lastSize = fs.Length;
                            }
                        }
                    }
                    catch { }
                    await System.Threading.Tasks.Task.Delay(500);
                }
            });

            // Wait with timeout (10 minutes)
            var timeout = Task.Delay(TimeSpan.FromMinutes(10));
            var exited = Task.Run(() => proc.WaitForExit());
            if (await Task.WhenAny(exited, timeout) == timeout)
            {
                try { proc.Kill(entireProcessTree: true); } catch { }
                Output?.Invoke(new LogEntry($"[{logPrefix}] ⏱ Превышено время ожидания (10 мин). Процесс остановлен.", Colors.Red));
                stdoutFile.Dispose();
                return -1;
            }

            await logTailer;
            stdoutFile.Dispose();

            if (proc.ExitCode != 0)
            {
                var hint = proc.ExitCode switch
                {
                    5 => " — файл заблокирован антивирусом.",
                    6 => " — ошибка скачивания. Проверьте интернет.",
                    7 => " — нет места на диске.",
                    8 => " — не удалось записать файлы. Отключите антивирус или выберите другой путь.",
                    9 => " — недостаточно прав. Запустите RGC от имени администратора.",
                    10 => " — ошибка Steam. Попробуйте позже.",
                    _ => ""
                };
                Output?.Invoke(new LogEntry($"[{logPrefix}] ❌ Ошибка (код: {proc.ExitCode}){hint}", Colors.Red));
            }
            return proc.ExitCode;
        }

        private bool ParseSteamOutput(string line, string logPrefix, bool guardAlreadySent,
            Process proc, Func<Task<string?>>? getGuardCode)
        {
            OnSteamOutput(line, logPrefix);

            // Parse steamcmd progress: [  0%] [ 50%] [100%]
            var m = System.Text.RegularExpressions.Regex.Match(line, @"\[\s*(\d+)\s*%\]");
            if (m.Success && int.TryParse(m.Groups[1].Value, out var pct))
                DownloadProgress?.Invoke(pct);

            if (!guardAlreadySent && getGuardCode != null &&
                (line.Contains("Steam Guard", StringComparison.OrdinalIgnoreCase) ||
                 line.Contains("Two-factor", StringComparison.OrdinalIgnoreCase) ||
                 line.Contains("guard code", StringComparison.OrdinalIgnoreCase) ||
                 line.Contains("код подтверждения", StringComparison.OrdinalIgnoreCase)))
            {
                _ = GuardCodePromptLoop(proc, getGuardCode);
                return true;
            }
            return false;
        }

        private static async System.Threading.Tasks.Task GuardCodePromptLoop(Process proc, Func<Task<string?>> getGuardCode)
        {
            try
            {
                var code = await getGuardCode();
                if (!string.IsNullOrEmpty(code))
                {
                    await proc.StandardInput.WriteLineAsync(code);
                    await proc.StandardInput.FlushAsync();
                }
            }
            catch { }
        }

        public async System.Threading.Tasks.Task DownloadWorkshopMod(string login, string pass, string modId,
            Func<Task<string?>>? getGuardCode = null)
        {
            var scriptPath = Path.Combine(Path.GetTempPath(), $"rgc_steamcmd_{Guid.NewGuid()}.txt");
            try
            {
                File.WriteAllText(scriptPath, $"login {login} {pass}\nworkshop_download_item 221100 {modId}\nquit");

                var exitCode = await RunSteamCmd($"+runscript \"{scriptPath}\"", "Workshop", getGuardCode);

                if (exitCode == 0)
                    Output?.Invoke(new LogEntry($"[Workshop] Мод {modId} загружен", Colors.LimeGreen));
                else
                {
                    var logFile = Path.Combine(SteamCmdDir, "logs", "stderr.txt");
                    if (File.Exists(logFile))
                    {
                        var err = File.ReadAllText(logFile, Encoding.UTF8);
                        if (!string.IsNullOrWhiteSpace(err))
                            Output?.Invoke(new LogEntry($"[Workshop] stderr: {err.Trim()}", Colors.Red));
                    }
                }
            }
            finally
            {
                if (File.Exists(scriptPath))
                    File.Delete(scriptPath);
            }
        }

        private async System.Threading.Tasks.Task DownloadSteamCmd()
        {
            if (!Directory.Exists(SteamCmdDir))
                Directory.CreateDirectory(SteamCmdDir);

            var zipPath = Path.Combine(SteamCmdDir, "steamcmd.zip");

            using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
            using var response = await client.GetAsync("https://steamcdn-a.akamaihd.net/client/installer/steamcmd.zip",
                HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1;
            using var contentStream = await response.Content.ReadAsStreamAsync();
            using var fileStream = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

            var buffer = new byte[8192];
            long readBytes = 0;
            int bytesRead;
            while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                await fileStream.WriteAsync(buffer, 0, bytesRead);
                readBytes += bytesRead;
                if (totalBytes > 0)
                    DownloadProgress?.Invoke((int)(readBytes * 100 / totalBytes));
            }

            ZipFile.ExtractToDirectory(zipPath, SteamCmdDir, overwriteFiles: true);
            File.Delete(zipPath);
        }

        private void OnSteamOutput(string? line, string prefix)
        {
            if (string.IsNullOrWhiteSpace(line)) return;
            var color = line.Contains("Error", StringComparison.OrdinalIgnoreCase) ? Colors.Red
                      : line.Contains("Success", StringComparison.OrdinalIgnoreCase) ? Colors.LimeGreen
                      : Colors.Silver;
            Output?.Invoke(new LogEntry($"[{prefix}] {line}", color));
        }
    }
}
