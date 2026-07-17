using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Highlighting;
using HL = ICSharpCode.AvalonEdit.Highlighting;
using RGC.Services;
using MessageBox = System.Windows.MessageBox;

namespace RGC
{
    public partial class FtpBrowserWindow : Window
    {
        private readonly string _host;
        private readonly int _port;
        private readonly string _username;
        private readonly string _password;
        private readonly string _localPath;
        private FtpTreeNode? _selectedNode;
        private TextEditor? _editor;
        private string? _editedFilePath;
        private bool _editorDirty;

        public string CurrentPath { get; private set; } = "/";

        public FtpBrowserWindow(string host, int port, string username, string password, string remotePath = "/", string localPath = "")
        {
            InitializeComponent();
            _host = host;
            _port = port;
            _username = username;
            _password = password;
            _localPath = localPath;
            CurrentPath = remotePath.StartsWith("/") ? remotePath : "/" + remotePath;

            BrowserTitle.Text = $"{_host}:{_port} — FTP Browser";
            if (!string.IsNullOrEmpty(_localPath))
            {
                StatusText.Text = $"Local: {_localPath}";
                SyncBtn.Visibility = Visibility.Visible;
            }
            InitEditor();
            ConnectAndLoad();
        }

        private void InitEditor()
        {
            var m = System.Windows.Media.Color.FromRgb;
            _editor = new TextEditor
            {
                ShowLineNumbers = true,
                FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                FontSize = 12,
                Background = new SolidColorBrush(m(0x1e, 0x1e, 0x2e)),
                Foreground = new SolidColorBrush(m(0xcd, 0xd6, 0xf4)),
                LineNumbersForeground = new SolidColorBrush(m(0x58, 0x5b, 0x70)),
                WordWrap = true,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            };
            _editor.TextArea.TextView.CurrentLineBackground = new SolidColorBrush(m(0x1a, 0x1a, 0x2a));
            _editor.TextChanged += (_, _) => { _editorDirty = true; UploadBtn.IsEnabled = true; };
            EditorContainer.Child = _editor;
        }

        private static HL.HighlightingColor Col(byte r, byte g, byte b) =>
            new() { Foreground = new HL.SimpleHighlightingBrush(System.Windows.Media.Color.FromRgb(r, g, b)) };

        private static IHighlightingDefinition CreateJsonHighlighting()
        {
            var main = new HL.HighlightingRuleSet();
            main.Rules.Add(new HL.HighlightingRule
            {
                Regex = new Regex(@"""[^""]+""\s*:"),
                Color = Col(0x89, 0xb4, 0xfa)
            });
            main.Rules.Add(new HL.HighlightingRule
            {
                Regex = new Regex(@"""(?:[^""\\]|\\.)*""(?=\s*[,}\]])"),
                Color = Col(0xa6, 0xe3, 0xa1)
            });
            main.Rules.Add(new HL.HighlightingRule
            {
                Regex = new Regex(@"(?<=\s|:|,)\b-?\d+(\.\d+)?([eE][+-]?\d+)?\b"),
                Color = Col(0xfa, 0xb3, 0x87)
            });
            main.Rules.Add(new HL.HighlightingRule
            {
                Regex = new Regex(@"\b(true|false)\b"),
                Color = Col(0xcb, 0xa6, 0xf7)
            });
            main.Rules.Add(new HL.HighlightingRule
            {
                Regex = new Regex(@"\bnull\b"),
                Color = Col(0x6c, 0x70, 0x8b)
            });
            main.Rules.Add(new HL.HighlightingRule
            {
                Regex = new Regex(@"[\[\]{}]"),
                Color = Col(0xf5, 0xc2, 0xe7)
            });
            return new JsonHighlightingDefinition(main);
        }

        private class JsonHighlightingDefinition : IHighlightingDefinition
        {
            public JsonHighlightingDefinition(HL.HighlightingRuleSet ruleSet) => MainRuleSet = ruleSet;
            public string Name => "JSON";
            public HL.HighlightingRuleSet MainRuleSet { get; }
            public HL.HighlightingRuleSet GetNamedRuleSet(string name) => name == "JSON" ? MainRuleSet : null!;
            public HL.HighlightingColor GetNamedColor(string name) => null!;
            public HL.HighlightingColor GetNamedHighlightingColor(string name) => null!;
            public IEnumerable<HL.HighlightingColor> NamedHighlightingColors => Enumerable.Empty<HL.HighlightingColor>();
            public IDictionary<string, string> Properties => new Dictionary<string, string>();
        }

        private static readonly IHighlightingDefinition JsonHighlightingDef = CreateJsonHighlighting();

        private void ApplyJsonHighlighting()
        {
            if (_editor == null) return;
            try { _editor.SyntaxHighlighting = JsonHighlightingDef; }
            catch { }
        }

        private void ValidateJson(string content)
        {
            try
            {
                JsonDocument.Parse(content);
                Log("✓ JSON valid");
                NotificationService.Show("✓ JSON валиден");
                FileInfoText.Text = "JSON валиден";
            }
            catch (JsonException ex)
            {
                var line = ex.LineNumber.HasValue ? $" line {ex.LineNumber + 1}" : "";
                Log($"✗ JSON error{line}: {ex.Message}");

                NotificationService.Show($"✗ JSON ошибка{line}");
                FileInfoText.Text = $"JSON ошибка{line}: {ex.Message}";
            }
        }

        private string FtpUrl(string path)
        {
            path = path.Replace("\\", "/");
            if (!path.StartsWith("/")) path = "/" + path;
            return $"ftp://{_host}:{_port}{path}";
        }

        private void Log(string msg)
        {
            var ts = DateTime.Now.ToString("HH:mm:ss");
            Dispatcher.Invoke(() =>
            {
                ConsoleBox.AppendText($"[{ts}] {msg}\n");
                ConsoleBox.ScrollToEnd();
            });
        }

        private void ConnectAndLoad()
        {
            Log($"Connecting to {_host}:{_port}...");
            StatusText.Text = "Connecting...";
            FileTree.Items.Clear();

            var root = new FtpTreeNode
            {
                Name = "/",
                FullPath = "/",
                IsDirectory = true,
                Icon = "📁"
            };
            root.Children.Add(new FtpTreeNode { Name = "(loading...)", FullPath = "" });
            FileTree.Items.Add(root);
            LoadDirectory(root);
        }

        private void LoadDirectory(FtpTreeNode parent)
        {
            parent.Children.Clear();
            try
            {
                var req = (FtpWebRequest)WebRequest.Create(FtpUrl(parent.FullPath));
                req.Method = WebRequestMethods.Ftp.ListDirectoryDetails;
                req.Credentials = new NetworkCredential(_username, _password);
                req.UsePassive = true;
                req.UseBinary = true;
                req.Timeout = 10000;

                using var resp = (FtpWebResponse)req.GetResponse();
                using var stream = resp.GetResponseStream();
                using var reader = new StreamReader(stream);

                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    var entry = ParseEntry(line, parent.FullPath);
                    if (entry != null)
                        parent.Children.Add(entry);
                }

                var sorted = parent.Children.OrderByDescending(c => c.IsDirectory).ThenBy(c => c.Name).ToList();
                parent.Children.Clear();
                foreach (var c in sorted)
                {
                    if (c.IsDirectory)
                        c.Children.Add(new FtpTreeNode { Name = "(loading...)", FullPath = "" });
                    parent.Children.Add(c);
                }

                var itemCount = parent.Children.Count;
                StatusText.Text = $"{parent.FullPath} — {itemCount} items";
                FileCountText.Text = $"{itemCount} items";
                CurrentPath = parent.FullPath;
                Log($"Loaded {itemCount} items from {parent.FullPath}");
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Error: {ex.Message}";
                Log($"Error loading {parent.FullPath}: {ex.Message}");
            }
        }

        private static FtpTreeNode? ParseEntry(string line, string parentPath)
        {
            parentPath = parentPath.TrimEnd('/');
            if (string.IsNullOrWhiteSpace(line)) return null;

            var u = Regex.Match(line, @"^([drwxst-]{10})\s+\d+\s+\S+\s+\S+\s+(\d+)\s+(\w{3}\s+\d{1,2}\s+(?:\d{1,2}:\d{2}|\d{4}))\s+(.+)$");
            if (u.Success)
            {
                var isDir = u.Groups[1].Value[0] == 'd';
                var name = u.Groups[4].Value;
                if (name == "." || name == "..") return null;
                var sb = long.Parse(u.Groups[2].Value);
                return new FtpTreeNode
                {
                    Name = isDir ? name : $"{name}  ({FormatSize(sb)})",
                    FullPath = $"{parentPath}/{name}",
                    IsDirectory = isDir,
                    Icon = isDir ? "📁" : "📄",
                    Size = FormatSize(sb),
                    SizeBytes = sb
                };
            }

            var d = Regex.Match(line, @"^(\d{2}-\d{2}-\d{2,4})\s+(\d{2}:\d{2}(?:AM|PM)?)\s+(<DIR>|\d+)\s+(.+)$");
            if (d.Success)
            {
                var isDir = d.Groups[3].Value == "<DIR>";
                var name = d.Groups[4].Value;
                if (name == "." || name == "..") return null;
                var sb = isDir ? 0 : long.Parse(d.Groups[3].Value);
                return new FtpTreeNode
                {
                    Name = isDir ? name : $"{name}  ({FormatSize(sb)})",
                    FullPath = $"{parentPath}/{name}",
                    IsDirectory = isDir,
                    Icon = isDir ? "📁" : "📄",
                    Size = FormatSize(sb),
                    SizeBytes = sb
                };
            }

            return null;
        }

        private static string FormatSize(long bytes)
        {
            return bytes < 1024 ? $"{bytes} B"
                : bytes < 1024 * 1024 ? $"{bytes / 1024.0:F1} KB"
                : $"{bytes / (1024.0 * 1024.0):F1} MB";
        }

        private void OnNodeExpanded(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is TreeViewItem item && item.DataContext is FtpTreeNode node && node.IsDirectory)
            {
                if (node.Children.Count == 1 && node.Children[0].FullPath == "")
                    LoadDirectory(node);
            }
        }

        private void FileTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            _selectedNode = e.NewValue as FtpTreeNode;
            var isFile = _selectedNode is { IsDirectory: false };

            DownloadBtn.IsEnabled = _selectedNode != null && !string.IsNullOrEmpty(_selectedNode.FullPath);
            DeleteBtn.IsEnabled = _selectedNode != null && !string.IsNullOrEmpty(_selectedNode.FullPath);
            RenameBtn.IsEnabled = _selectedNode != null && !string.IsNullOrEmpty(_selectedNode.FullPath);

            if (isFile)
                LoadFileIntoEditor(_selectedNode!);
            else if (_selectedNode != null)
                ClearEditor();
        }

        private void LoadFileIntoEditor(FtpTreeNode node)
        {
            try
            {
                var localFile = Path.Combine(Path.GetTempPath(), Path.GetFileName(node.FullPath));

                var req = (FtpWebRequest)WebRequest.Create(FtpUrl(node.FullPath));
                req.Method = WebRequestMethods.Ftp.DownloadFile;
                req.Credentials = new NetworkCredential(_username, _password);
                req.UsePassive = true;
                req.UseBinary = true;
                req.Timeout = 15000;

                using var resp = (FtpWebResponse)req.GetResponse();
                using var stream = resp.GetResponseStream();
                using (var fs = new FileStream(localFile, FileMode.Create))
                {
                    stream.CopyTo(fs);
                }

                var content = File.ReadAllText(localFile);
                _editor!.Text = content;
                _editedFilePath = node.FullPath;
                _editorDirty = false;
                UploadBtn.IsEnabled = true;
                ValidateBtn.IsEnabled = true;
                FileNameTab.Text = $"📄 {node.FullPath}";

                // Auto-apply JSON highlighting for .json files
                if (node.FullPath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                    ApplyJsonHighlighting();
                else
                    _editor.SyntaxHighlighting = null;
                FileInfoText.Text = $"{node.Name}  |  {FormatSize(node.SizeBytes)}  |  loaded for editing";
                Log($"Loaded {node.FullPath} into editor ({FormatSize(node.SizeBytes)})");
            }
            catch (Exception ex)
            {
                Log($"Error loading file: {ex.Message}");
                _editor!.Text = $"Error loading file:\n{ex.Message}";
                _editedFilePath = null;
            }
        }

        private void ClearEditor()
        {
            _editor!.Text = "";
            _editorDirty = false;
            _editedFilePath = null;
            UploadBtn.IsEnabled = false;
            ValidateBtn.IsEnabled = false;
            FileInfoText.Text = "";
            FileNameTab.Text = "";
        }

        // === Toolbar actions ===

        private void Sync_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_localPath))
            {
                MessageBox.Show(this, "Local path is not set.", "Sync", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var localExists = Directory.Exists(_localPath);
            var msg = localExists
                ? $"Sync with local folder?\n\nLocal: {_localPath}\nRemote: {CurrentPath}\n\n" +
                  "Yes → Upload local files to server (overwrites remote)\n" +
                  "No  → Download remote files to local (overwrites local)"
                : $"Local folder not found:\n{_localPath}\n\n" +
                  "Do you want to create it and download remote files?";

            var result = MessageBox.Show(this, msg, "Confirm Sync",
                MessageBoxButton.YesNoCancel, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                if (!localExists)
                {
                    MessageBox.Show(this, "Local folder doesn't exist. Create it first or choose Download.",
                        "Sync", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                var confirm = MessageBox.Show(this,
                    $"Upload ALL files from:\n{_localPath}\n\nto:\n{CurrentPath}\n\n" +
                    "Remote files with the same name will be overwritten. Continue?",
                    "Upload Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (confirm == MessageBoxResult.Yes)
                    SyncUpload(_localPath, CurrentPath);
            }
            else if (result == MessageBoxResult.No)
            {
                if (!localExists)
                {
                    try { Directory.CreateDirectory(_localPath); }
                    catch (Exception ex)
                    {
                        MessageBox.Show(this, $"Cannot create local folder:\n{ex.Message}",
                            "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }
                var confirm = MessageBox.Show(this,
                    $"Download ALL files from:\n{CurrentPath}\n\nto:\n{_localPath}\n\n" +
                    "Local files with the same name will be overwritten. Continue?",
                    "Download Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (confirm == MessageBoxResult.Yes)
                    SyncDownload(CurrentPath, _localPath);
            }
        }

        private void SyncUpload(string localDir, string remoteDir)
        {
            try
            {
                var files = Directory.GetFiles(localDir, "*", SearchOption.AllDirectories);
                int count = 0;
                foreach (var file in files)
                {
                    var relative = Path.GetRelativePath(localDir, file).Replace("\\", "/");
                    var remoteFile = (remoteDir.TrimEnd('/') + "/" + relative).Replace("//", "/");
                    var remoteParent = Path.GetDirectoryName(remoteFile)!.Replace("\\", "/");

                    EnsureRemoteDir(remoteParent);

                    var content = File.ReadAllBytes(file);
                    var req = (FtpWebRequest)WebRequest.Create(FtpUrl(remoteFile));
                    req.Method = WebRequestMethods.Ftp.UploadFile;
                    req.Credentials = new NetworkCredential(_username, _password);
                    req.UsePassive = true;
                    req.UseBinary = true;
                    req.ContentLength = content.Length;
                    using var stream = req.GetRequestStream();
                    stream.Write(content, 0, content.Length);
                    count++;
                    Log($"↑ {relative}");
                }
                Log($"✓ Sync upload complete: {count} files");
                NotificationService.Show($"✅ Uploaded {count} files to server");
                ConnectAndLoad();
            }
            catch (Exception ex)
            {
                Log($"✗ Sync upload error: {ex.Message}");
                MessageBox.Show(this, $"Sync upload error:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void EnsureRemoteDir(string remoteDir)
        {
            if (string.IsNullOrEmpty(remoteDir) || remoteDir == "/" || remoteDir == ".") return;
            try
            {
                var req = (FtpWebRequest)WebRequest.Create(FtpUrl(remoteDir));
                req.Method = WebRequestMethods.Ftp.MakeDirectory;
                req.Credentials = new NetworkCredential(_username, _password);
                req.UsePassive = true;
                req.UseBinary = true;
                req.GetResponse().Close();
            }
            catch { }
        }

        private void SyncDownload(string remoteDir, string localDir)
        {
            try
            {
                int count = 0;
                SyncDownloadRecursive(remoteDir, localDir, ref count);
                Log($"✓ Sync download complete: {count} files to {localDir}");
                NotificationService.Show($"✅ Downloaded {count} files to local");
            }
            catch (Exception ex)
            {
                Log($"✗ Sync download error: {ex.Message}");
                MessageBox.Show(this, $"Sync download error:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SyncDownloadRecursive(string remoteDir, string localDir, ref int count)
        {
            Directory.CreateDirectory(localDir);
            var entries = ListFtpEntries(remoteDir);
            foreach (var entry in entries)
            {
                if (entry.IsDirectory)
                {
                    SyncDownloadRecursive(entry.FullPath, Path.Combine(localDir, entry.Name), ref count);
                }
                else
                {
                    var localFile = Path.Combine(localDir, entry.Name);
                    try
                    {
                        var req = (FtpWebRequest)WebRequest.Create(FtpUrl(entry.FullPath));
                        req.Method = WebRequestMethods.Ftp.DownloadFile;
                        req.Credentials = new NetworkCredential(_username, _password);
                        req.UsePassive = true;
                        req.UseBinary = true;
                        using var resp = (FtpWebResponse)req.GetResponse();
                        using var stream = resp.GetResponseStream();
                        using var fs = File.Create(localFile);
                        stream.CopyTo(fs);
                        count++;
                        Log($"↓ {entry.FullPath}");
                    }
                    catch (Exception ex)
                    {
                        Log($"✗ Failed {entry.FullPath}: {ex.Message}");
                    }
                }
            }
        }

        private List<(string Name, string FullPath, bool IsDirectory)> ListFtpEntries(string dir)
        {
            var result = new List<(string, string, bool)>();
            try
            {
                var req = (FtpWebRequest)WebRequest.Create(FtpUrl(dir));
                req.Method = WebRequestMethods.Ftp.ListDirectoryDetails;
                req.Credentials = new NetworkCredential(_username, _password);
                req.UsePassive = true;
                req.UseBinary = true;
                req.Timeout = 10000;
                using var resp = (FtpWebResponse)req.GetResponse();
                using var reader = new StreamReader(resp.GetResponseStream());
                while (reader.ReadLine() is string line)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    var name = ParseFtpEntryName(line);
                    if (name == "." || name == "..") continue;
                    var isDir = line.StartsWith("d") || line.ToUpperInvariant().Contains("<DIR>");
                    var fullPath = (dir.TrimEnd('/') + "/" + name).Replace("//", "/");
                    result.Add((name, fullPath, isDir));
                }
            }
            catch (Exception ex)
            {
                Log($"✗ List error {dir}: {ex.Message}");
            }
            return result;
        }

        private static string ParseFtpEntryName(string line)
        {
            // Unix format: "drwxr-xr-x 1 user group 0 Jan 01 00:00 dirname"
            // Windows format: "01-01-25  12:00AM       <DIR>          dirname"
            if (line.ToUpperInvariant().Contains("<DIR>") || line.Contains('<'))
            {
                var parts = line.Split(new[] { '<' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 1)
                    return parts[^1].Trim();
            }
            var match = Regex.Match(line, @"^\S+\s+\d+\s+\S+\s+\S+\s+\d+\s+\S+\s+\S+\s+\S+\s+(.+)$");
            if (match.Success)
                return match.Groups[1].Value;
            // MS FTP: "01-01-25  12:00AM                12345 filename.ext"
            var msMatch = Regex.Match(line, @"^\d{2}-\d{2}-\d{2}\s+\S+\s+(?:\<\S+\>\s+)?(\S.*)$");
            if (msMatch.Success)
                return msMatch.Groups[1].Value.Trim();
            return line.Trim();
        }

        private void Download_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedNode == null) return;
            if (_selectedNode.IsDirectory)
                DownloadFolder(_selectedNode);
            else
                DownloadFile(_selectedNode);
        }

        private void DownloadFile(FtpTreeNode node)
        {
            try
            {
                var dlg = new System.Windows.Forms.SaveFileDialog
                {
                    FileName = Path.GetFileName(node.FullPath),
                    Filter = "All files (*.*)|*.*"
                };
                if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

                var req = (FtpWebRequest)WebRequest.Create(FtpUrl(node.FullPath));
                req.Method = WebRequestMethods.Ftp.DownloadFile;
                req.Credentials = new NetworkCredential(_username, _password);
                req.UsePassive = true;
                req.UseBinary = true;
                req.Timeout = 15000;

                using var resp = (FtpWebResponse)req.GetResponse();
                using var stream = resp.GetResponseStream();
                using var fs = new FileStream(dlg.FileName, FileMode.Create);
                stream.CopyTo(fs);

                Log($"Downloaded {node.FullPath} → {dlg.FileName} ({FormatSize(node.SizeBytes)})");
            }
            catch (Exception ex)
            {
                Log($"Download error: {ex.Message}");
            }
        }

        private void DownloadFolder(FtpTreeNode node)
        {
            try
            {
                using var dlg = new System.Windows.Forms.FolderBrowserDialog();
                dlg.Description = $"Select folder to download \"{node.Name}\" to";
                if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

                var localRoot = Path.Combine(dlg.SelectedPath, node.Name);
                Directory.CreateDirectory(localRoot);
                DownloadFolderRecursive(node, localRoot);
                Log($"Downloaded folder {node.FullPath} → {localRoot}");
            }
            catch (Exception ex)
            {
                Log($"Folder download error: {ex.Message}");
            }
        }

        private void DownloadFolderRecursive(FtpTreeNode node, string localPath)
        {
            // Ensure this directory's children are loaded
            if (node.Children.Count == 0 || (node.Children.Count == 1 && node.Children[0].FullPath == ""))
                LoadDirectory(node);

            foreach (var child in node.Children)
            {
                if (child.IsDirectory)
                {
                    var subDir = Path.Combine(localPath, child.Name);
                    Directory.CreateDirectory(subDir);
                    DownloadFolderRecursive(child, subDir);
                }
                else
                {
                    var localFile = Path.Combine(localPath, Path.GetFileName(child.FullPath));
                    try
                    {
                        var req = (FtpWebRequest)WebRequest.Create(FtpUrl(child.FullPath));
                        req.Method = WebRequestMethods.Ftp.DownloadFile;
                        req.Credentials = new NetworkCredential(_username, _password);
                        req.UsePassive = true;
                        req.UseBinary = true;
                        req.Timeout = 15000;

                        using var resp = (FtpWebResponse)req.GetResponse();
                        using var stream = resp.GetResponseStream();
                        using var fs = new FileStream(localFile, FileMode.Create);
                        stream.CopyTo(fs);
                    }
                    catch (Exception ex)
                    {
                        Log($"  Failed: {child.FullPath} — {ex.Message}");
                    }
                }
            }
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedNode == null) return;
            var msg = $"Delete \"{_selectedNode.FullPath}\"?";
            if (MessageBox.Show(this, msg, "Confirm Delete",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            try
            {
                if (_selectedNode.IsDirectory)
                    DeleteFtpDirectory(_selectedNode);
                else
                    DeleteFtpFile(_selectedNode);

                Log($"Deleted {_selectedNode.FullPath}");
                ConnectAndLoad();
            }
            catch (Exception ex)
            {
                Log($"Delete error: {ex.Message}");
            }
        }

        private void DeleteFtpFile(FtpTreeNode node)
        {
            var req = (FtpWebRequest)WebRequest.Create(FtpUrl(node.FullPath));
            req.Method = WebRequestMethods.Ftp.DeleteFile;
            req.Credentials = new NetworkCredential(_username, _password);
            req.UsePassive = true;
            req.GetResponse()?.Close();
        }

        private void DeleteFtpDirectory(FtpTreeNode node)
        {
            // Ensure children loaded
            if (node.Children.Count == 0 || (node.Children.Count == 1 && node.Children[0].FullPath == ""))
                LoadDirectory(node);

            // Delete children first
            foreach (var child in node.Children.ToList())
            {
                if (child.IsDirectory)
                    DeleteFtpDirectory(child);
                else
                    DeleteFtpFile(child);
            }

            // Remove the empty directory
            try
            {
                var req = (FtpWebRequest)WebRequest.Create(FtpUrl(node.FullPath));
                req.Method = WebRequestMethods.Ftp.RemoveDirectory;
                req.Credentials = new NetworkCredential(_username, _password);
                req.UsePassive = true;
                req.GetResponse()?.Close();
            }
            catch (Exception ex)
            {
                Log($"  Failed to remove directory {node.FullPath}: {ex.Message}");
            }
        }

        private void Rename_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedNode == null) return;

            var oldName = _selectedNode.Name.Contains("  (") 
                ? _selectedNode.Name[.._selectedNode.Name.LastIndexOf("  (", StringComparison.Ordinal)] 
                : _selectedNode.Name;

            var newName = Microsoft.VisualBasic.Interaction.InputBox(
                "New name:", "Rename", oldName, -1, -1);
            if (string.IsNullOrWhiteSpace(newName) || newName == oldName) return;

            try
            {
                var parentPath = System.IO.Path.GetDirectoryName(_selectedNode.FullPath)?.Replace("\\", "/") ?? "/";
                var newPath = $"{parentPath}/{newName}";

                var req = (FtpWebRequest)WebRequest.Create(FtpUrl(_selectedNode.FullPath));
                req.Method = WebRequestMethods.Ftp.Rename;
                req.RenameTo = newName;
                req.Credentials = new NetworkCredential(_username, _password);
                req.UsePassive = true;
                req.GetResponse()?.Close();

                Log($"Renamed {_selectedNode.FullPath} → {newPath}");
                ConnectAndLoad();
            }
            catch (Exception ex)
            {
                Log($"Rename error: {ex.Message}");
            }
        }

        private async void Upload_Click(object sender, RoutedEventArgs e)
        {
            if (_editedFilePath == null || _editor == null) return;
            var content = _editor.Text;
            UploadBtn.IsEnabled = false;
            try
            {
                var path = _editedFilePath;
                Log($"Uploading to: {FtpUrl(path)}");
                await Task.Run(() => UploadFile(path, content));
                _editorDirty = false;
                Dispatcher.Invoke(() =>
                {
                    StatusText.Text = "Файлы успешно отправлены";
                    UploadBtn.IsEnabled = true;
                    RGC.Services.NotificationService.Show("✅ Файлы успешно отправлены");
                });
            }
            catch (Exception ex)
            {
                Log($"Apply error: {ex.Message} | Path: {_editedFilePath} | URL: {FtpUrl(_editedFilePath ?? "?")}");
                Dispatcher.Invoke(() => UploadBtn.IsEnabled = true);
            }
        }

        private void UploadFile(string remotePath, string content)
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(content);
            var req = (FtpWebRequest)WebRequest.Create(FtpUrl(remotePath));
            req.Method = WebRequestMethods.Ftp.UploadFile;
            req.Credentials = new NetworkCredential(_username, _password);
            req.UsePassive = true;
            req.UseBinary = true;
            req.ContentLength = bytes.Length;

            using (var reqStream = req.GetRequestStream())
            {
                reqStream.Write(bytes, 0, bytes.Length);
            }

            using (var resp = (FtpWebResponse)req.GetResponse())
            {
                _editorDirty = false;
                Log($"Uploaded {remotePath} ({FormatSize(content.Length)}) — {resp.StatusDescription}");
            }
        }

        private void Validate_Click(object sender, RoutedEventArgs e)
        {
            if (_editor == null) return;
            ValidateJson(_editor.Text);
        }

        // === Event handlers ===

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            ConnectAndLoad();
        }

        private void ClearConsole_Click(object sender, RoutedEventArgs e)
        {
            ConsoleBox.Clear();
        }

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            new ProjectLibraryWindow().Show();
            Close();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            if (_editorDirty)
            {
                var result = MessageBox.Show(this,
                    "File has unsaved changes. Upload before closing?",
                    "Unsaved Changes", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                    Upload_Click(sender, e);
                else if (result == MessageBoxResult.Cancel)
                    return;
            }
            Close();
        }
    }

    public class FtpTreeNode
    {
        public string Name { get; set; } = "";
        public string FullPath { get; set; } = "";
        public bool IsDirectory { get; set; }
        public string Icon { get; set; } = "📄";
        public string Size { get; set; } = "";
        public long SizeBytes { get; set; }
        public ObservableCollection<FtpTreeNode> Children { get; } = new();
    }
}
