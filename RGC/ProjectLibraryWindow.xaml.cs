using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using RGC.Models;
using RGC.Services;
using Button = System.Windows.Controls.Button;
using Image = System.Windows.Controls.Image;
using TextBox = System.Windows.Controls.TextBox;
using MessageBox = System.Windows.MessageBox;
using Application = System.Windows.Application;
using Localization = RGC.Services.Localization;

namespace RGC
{
    public partial class ProjectLibraryWindow : Window
    {
        public static Project? LastOpenedProject { get; private set; }
        private static readonly System.Windows.Media.Color[] AccentColors = new[]
        {
            System.Windows.Media.Color.FromRgb(0x89, 0xb4, 0xfa),
            System.Windows.Media.Color.FromRgb(0xa6, 0xe3, 0xa1),
            System.Windows.Media.Color.FromRgb(0xf5, 0xc2, 0xe7),
            System.Windows.Media.Color.FromRgb(0xfa, 0xe8, 0xb0),
            System.Windows.Media.Color.FromRgb(0xba, 0xc7, 0xfa),
            System.Windows.Media.Color.FromRgb(0x94, 0xe2, 0xd5),
        };

        public ProjectLibraryWindow()
        {
            InitializeComponent();
            ApplyLang();
            LoadProjects();
            LoadFtpProjects();
            LoadTools();
        }

        private void ApplyLang()
        {
            LibTitle.Text = Localization.T("lib.title");
            NewProjectBtn.Content = Localization.T("lib.new");
            LocalHeader.Text = Localization.T("lib.local_title");
            FtpHeader.Text = Localization.T("lib.ftp_title");
            ToolsHeader.Text = Localization.T("lib.tools_title");
        }

        private void LoadProjects()
        {
            LocalPanel.Children.Clear();
            var projects = ProjectService.LoadAll();
            projects = projects.OrderByDescending(p => p.LastOpenedAt).ToList();

            for (var i = 0; i < projects.Count; i++)
                LocalPanel.Children.Add(CreateLocalCard(projects[i], i));
        }

        private void LoadFtpProjects()
        {
            FtpPanel.Children.Clear();
            var ftpList = FtpProjectService.LoadAll();
            ftpList = ftpList.OrderByDescending(p => p.LastOpenedAt).ToList();

            for (var i = 0; i < ftpList.Count; i++)
                FtpPanel.Children.Add(CreateFtpCard(ftpList[i], i));
        }

        private void LoadTools()
        {
            ToolsPanel.Children.Clear();
            var tools = new[]
            {
                new ToolItem { Name = "SteamCMD", Icon = "⬇", Description = Localization.T("tools.steamcmd"), Action = "steamcmd" },
                new ToolItem { Name = "Config Converter", Icon = "🔄", Description = Localization.T("tools.configconv"), Action = "configconv" },
            };
            for (var i = 0; i < tools.Length; i++)
                ToolsPanel.Children.Add(CreateToolCard(tools[i], i));
        }

        private Border CreateLocalCard(Project project, int index)
        {
            var c = System.Windows.Media.Color.FromRgb;
            var accent = AccentColors[index % AccentColors.Length];

            var serverOk = !string.IsNullOrEmpty(project.ServerPath)
                && System.IO.Directory.Exists(project.ServerPath);

            var dayzVersion = "";
            if (serverOk)
            {
                try
                {
                    var exe = System.IO.Path.Combine(project.ServerPath, "DayZServer_x64.exe");
                    if (System.IO.File.Exists(exe))
                    {
                        var vi = System.Diagnostics.FileVersionInfo.GetVersionInfo(exe);
                        dayzVersion = vi.ProductVersion?.Split(' ')[0] ?? vi.FileVersion ?? "";
                        if (dayzVersion.Length > 10) dayzVersion = dayzVersion[..10];
                    }
                }
                catch { }
            }

            var entranceTransform = new System.Windows.Media.TranslateTransform(0, 30);
            var entranceAnim = new System.Windows.Media.Animation.DoubleAnimation
            {
                From = 30, To = 0,
                Duration = TimeSpan.FromMilliseconds(400 + index * 60),
                EasingFunction = new System.Windows.Media.Animation.QuadraticEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
            };
            var fadeAnim = new System.Windows.Media.Animation.DoubleAnimation
            {
                From = 0, To = 0.95,
                Duration = TimeSpan.FromMilliseconds(400 + index * 60),
                EasingFunction = new System.Windows.Media.Animation.QuadraticEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
            };

            var avatar = new Border
            {
                Width = 44, Height = 44,
                CornerRadius = new System.Windows.CornerRadius(12),
                Background = new System.Windows.Media.SolidColorBrush(c(0x31, 0x32, 0x44)),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
                VerticalAlignment = System.Windows.VerticalAlignment.Top,
                Margin = new System.Windows.Thickness(20, 20, 0, 0),
                Child = new Image
                {
                    Source = new System.Windows.Media.Imaging.BitmapImage(
                        new Uri("pack://application:,,,/project_icon.png")),
                    Width = 24, Height = 24,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    VerticalAlignment = System.Windows.VerticalAlignment.Center
                }
            };

            var nameText = new TextBlock
            {
                Text = project.Name,
                FontSize = 17,
                FontWeight = FontWeights.SemiBold,
                Foreground = new System.Windows.Media.SolidColorBrush(c(0xcd, 0xd6, 0xf4)),
                Margin = new System.Windows.Thickness(72, 22, 20, 0),
                TextTrimming = TextTrimming.CharacterEllipsis,
                Height = 22,
                VerticalAlignment = System.Windows.VerticalAlignment.Top
            };

            var statusText = new TextBlock
            {
                Text = serverOk ? Localization.T("lib.configured") : Localization.T("lib.not_configured"),
                FontSize = 10,
                Foreground = new System.Windows.Media.SolidColorBrush(serverOk ? c(0xa6, 0xe3, 0xa1) : c(0x6c, 0x70, 0x8b)),
                TextTrimming = TextTrimming.CharacterEllipsis
            };

            var pathFull = serverOk ? project.ServerPath : Localization.T("lib.no_path");
            var pathText = new TextBlock
            {
                Text = pathFull,
                FontSize = 9,
                Foreground = new System.Windows.Media.SolidColorBrush(c(0x58, 0x5b, 0x70)),
                TextTrimming = TextTrimming.CharacterEllipsis,
                ToolTip = pathFull
            };

            var infoText = new TextBlock
            {
                FontSize = 9,
                Foreground = new System.Windows.Media.SolidColorBrush(c(0x58, 0x5b, 0x70)),
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            var lastOpen = string.Format(Localization.T("lib.last_open"), project.LastOpenedAt.ToString("dd MMM yyyy"));
            if (!string.IsNullOrEmpty(dayzVersion))
                infoText.Text = $"{string.Format(Localization.T("lib.dayz_version"), dayzVersion)} · {lastOpen}";
            else
                infoText.Text = lastOpen;

            var openBtn = new Button
            {
                Content = Localization.T("lib.open"),
                Height = 30,
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Background = new System.Windows.Media.SolidColorBrush(accent),
                Foreground = new System.Windows.Media.SolidColorBrush(c(0x1e, 0x1e, 0x2e)),
                BorderThickness = new System.Windows.Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand,
                Padding = new System.Windows.Thickness(14, 0, 14, 0),
                Margin = new System.Windows.Thickness(20, 0, 20, 14),
                VerticalAlignment = System.Windows.VerticalAlignment.Bottom,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch
            };
            openBtn.Click += (_, _) => OpenProject(project);

            var deleteBtn = new Button
            {
                Content = "✕",
                Width = 24, Height = 24,
                Background = System.Windows.Media.Brushes.Transparent,
                Foreground = new System.Windows.Media.SolidColorBrush(c(0x58, 0x5b, 0x70)),
                BorderThickness = new System.Windows.Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand,
                FontSize = 11,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
                VerticalAlignment = System.Windows.VerticalAlignment.Top,
                Margin = new System.Windows.Thickness(0, 12, 12, 0),
                Opacity = 0.4
            };
            deleteBtn.Click += (_, _) => DeleteProject(project);
            deleteBtn.MouseEnter += (_, _) => { deleteBtn.Foreground = new System.Windows.Media.SolidColorBrush(c(0xf3, 0x8b, 0xa8)); deleteBtn.Opacity = 1; };
            deleteBtn.MouseLeave += (_, _) => { deleteBtn.Foreground = new System.Windows.Media.SolidColorBrush(c(0x58, 0x5b, 0x70)); deleteBtn.Opacity = 0.4; };

            var statusDot = new Border
            {
                Width = 8, Height = 8,
                CornerRadius = new System.Windows.CornerRadius(4),
                Background = new System.Windows.Media.SolidColorBrush(serverOk ? c(0xa6, 0xe3, 0xa1) : c(0x6c, 0x70, 0x8b)),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
                VerticalAlignment = System.Windows.VerticalAlignment.Top,
                Margin = new System.Windows.Thickness(0, 14, 46, 0)
            };

            var infoStack = new StackPanel
            {
                Margin = new System.Windows.Thickness(72, 46, 20, 0),
                VerticalAlignment = System.Windows.VerticalAlignment.Top
            };
            infoStack.Children.Add(statusText);
            infoStack.Children.Add(pathText);
            infoStack.Children.Add(infoText);

            var innerGrid = new Grid();
            innerGrid.Children.Add(avatar);
            innerGrid.Children.Add(nameText);
            innerGrid.Children.Add(infoStack);
            innerGrid.Children.Add(openBtn);
            innerGrid.Children.Add(deleteBtn);
            innerGrid.Children.Add(statusDot);

            var card = new Border
            {
                Width = 250, Height = 220,
                Margin = new System.Windows.Thickness(8, 8, 28, 28),
                Background = new System.Windows.Media.SolidColorBrush(c(0x1e, 0x1e, 0x2e)),
                CornerRadius = new System.Windows.CornerRadius(14),
                BorderThickness = new System.Windows.Thickness(1),
                BorderBrush = new System.Windows.Media.SolidColorBrush(c(0x31, 0x32, 0x44)),
                Child = innerGrid,
                Opacity = 0
            };
            card.RenderTransform = entranceTransform;

            card.Loaded += (_, _) =>
            {
                entranceTransform.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, entranceAnim);
                card.BeginAnimation(System.Windows.UIElement.OpacityProperty, fadeAnim);
            };

            var glowAnim = new System.Windows.Media.Animation.ColorAnimation
            {
                To = c(0x89, 0xb4, 0xfa),
                Duration = TimeSpan.FromMilliseconds(150),
                EasingFunction = new System.Windows.Media.Animation.QuadraticEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
            };
            var unglowAnim = new System.Windows.Media.Animation.ColorAnimation
            {
                To = c(0x31, 0x32, 0x44),
                Duration = TimeSpan.FromMilliseconds(150),
                EasingFunction = new System.Windows.Media.Animation.QuadraticEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
            };
            card.MouseEnter += (_, _) =>
            {
                card.Background = new System.Windows.Media.SolidColorBrush(c(0x26, 0x26, 0x38));
                card.BorderBrush.BeginAnimation(System.Windows.Media.SolidColorBrush.ColorProperty, glowAnim);
            };
            card.MouseLeave += (_, _) =>
            {
                card.Background = new System.Windows.Media.SolidColorBrush(c(0x1e, 0x1e, 0x2e));
                card.BorderBrush.BeginAnimation(System.Windows.Media.SolidColorBrush.ColorProperty, unglowAnim);
            };
            card.MouseDown += (_, _) => OpenProject(project);

            return card;
        }

        private Border CreateFtpCard(FtpProject ftp, int index)
        {
            var c = System.Windows.Media.Color.FromRgb;
            var accent = AccentColors[(index + AccentColors.Length / 2) % AccentColors.Length];

            var entranceTransform = new System.Windows.Media.TranslateTransform(0, 30);
            var entranceAnim = new System.Windows.Media.Animation.DoubleAnimation
            {
                From = 30, To = 0,
                Duration = TimeSpan.FromMilliseconds(400 + index * 60),
                EasingFunction = new System.Windows.Media.Animation.QuadraticEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
            };
            var fadeAnim = new System.Windows.Media.Animation.DoubleAnimation
            {
                From = 0, To = 0.95,
                Duration = TimeSpan.FromMilliseconds(400 + index * 60),
                EasingFunction = new System.Windows.Media.Animation.QuadraticEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
            };

            var avatar = new Border
            {
                Width = 44, Height = 44,
                CornerRadius = new System.Windows.CornerRadius(12),
                Background = new System.Windows.Media.SolidColorBrush(c(0x31, 0x32, 0x44)),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
                VerticalAlignment = System.Windows.VerticalAlignment.Top,
                Margin = new System.Windows.Thickness(20, 20, 0, 0),
                Child = new TextBlock
                {
                    Text = "🌐",
                    FontSize = 20,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    VerticalAlignment = System.Windows.VerticalAlignment.Center
                }
            };

            var nameText = new TextBlock
            {
                Text = ftp.Name,
                FontSize = 17,
                FontWeight = FontWeights.SemiBold,
                Foreground = new System.Windows.Media.SolidColorBrush(c(0xcd, 0xd6, 0xf4)),
                Margin = new System.Windows.Thickness(72, 22, 20, 0),
                TextTrimming = TextTrimming.CharacterEllipsis,
                Height = 22,
                VerticalAlignment = System.Windows.VerticalAlignment.Top
            };

            var hostText = new TextBlock
            {
                Text = $"{ftp.Host}:{ftp.Port}",
                FontSize = 10,
                Foreground = new System.Windows.Media.SolidColorBrush(c(0xa6, 0xe3, 0xa1)),
                TextTrimming = TextTrimming.CharacterEllipsis
            };

            var remotePath = new TextBlock
            {
                Text = ftp.RemotePath,
                FontSize = 9,
                Foreground = new System.Windows.Media.SolidColorBrush(c(0x58, 0x5b, 0x70)),
                TextTrimming = TextTrimming.CharacterEllipsis,
                ToolTip = ftp.RemotePath
            };

            var infoText = new TextBlock
            {
                Text = string.Format(Localization.T("lib.last_open"), ftp.LastOpenedAt.ToString("dd MMM yyyy")),
                FontSize = 9,
                Foreground = new System.Windows.Media.SolidColorBrush(c(0x58, 0x5b, 0x70)),
                TextTrimming = TextTrimming.CharacterEllipsis
            };

            var openBtn = new Button
            {
                Content = Localization.T("ftp.settings"),
                Height = 30,
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Background = new System.Windows.Media.SolidColorBrush(accent),
                Foreground = new System.Windows.Media.SolidColorBrush(c(0x1e, 0x1e, 0x2e)),
                BorderThickness = new System.Windows.Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand,
                Padding = new System.Windows.Thickness(14, 0, 14, 0),
                Margin = new System.Windows.Thickness(20, 0, 20, 14),
                VerticalAlignment = System.Windows.VerticalAlignment.Bottom,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch
            };
            openBtn.Click += (_, _) => OpenFtpSettings(ftp);

            var deleteBtn = new Button
            {
                Content = "✕",
                Width = 24, Height = 24,
                Background = System.Windows.Media.Brushes.Transparent,
                Foreground = new System.Windows.Media.SolidColorBrush(c(0x58, 0x5b, 0x70)),
                BorderThickness = new System.Windows.Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand,
                FontSize = 11,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
                VerticalAlignment = System.Windows.VerticalAlignment.Top,
                Margin = new System.Windows.Thickness(0, 12, 12, 0),
                Opacity = 0.4
            };
            deleteBtn.Click += (_, _) => DeleteFtpProject(ftp);
            deleteBtn.MouseEnter += (_, _) => { deleteBtn.Foreground = new System.Windows.Media.SolidColorBrush(c(0xf3, 0x8b, 0xa8)); deleteBtn.Opacity = 1; };
            deleteBtn.MouseLeave += (_, _) => { deleteBtn.Foreground = new System.Windows.Media.SolidColorBrush(c(0x58, 0x5b, 0x70)); deleteBtn.Opacity = 0.4; };

            var infoStack = new StackPanel
            {
                Margin = new System.Windows.Thickness(72, 46, 20, 0),
                VerticalAlignment = System.Windows.VerticalAlignment.Top
            };
            infoStack.Children.Add(hostText);
            infoStack.Children.Add(remotePath);
            infoStack.Children.Add(infoText);

            var innerGrid = new Grid();
            innerGrid.Children.Add(avatar);
            innerGrid.Children.Add(nameText);
            innerGrid.Children.Add(infoStack);
            innerGrid.Children.Add(openBtn);
            innerGrid.Children.Add(deleteBtn);

            var card = new Border
            {
                Width = 250, Height = 220,
                Margin = new System.Windows.Thickness(8, 8, 28, 28),
                Background = new System.Windows.Media.SolidColorBrush(c(0x1e, 0x1e, 0x2e)),
                CornerRadius = new System.Windows.CornerRadius(14),
                BorderThickness = new System.Windows.Thickness(1),
                BorderBrush = new System.Windows.Media.SolidColorBrush(c(0x31, 0x32, 0x44)),
                Child = innerGrid,
                Opacity = 0
            };
            card.RenderTransform = entranceTransform;

            card.Loaded += (_, _) =>
            {
                entranceTransform.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, entranceAnim);
                card.BeginAnimation(System.Windows.UIElement.OpacityProperty, fadeAnim);
            };

            var glowAnim = new System.Windows.Media.Animation.ColorAnimation
            {
                To = c(0x89, 0xb4, 0xfa),
                Duration = TimeSpan.FromMilliseconds(150),
                EasingFunction = new System.Windows.Media.Animation.QuadraticEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
            };
            var unglowAnim = new System.Windows.Media.Animation.ColorAnimation
            {
                To = c(0x31, 0x32, 0x44),
                Duration = TimeSpan.FromMilliseconds(150),
                EasingFunction = new System.Windows.Media.Animation.QuadraticEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
            };
            card.MouseEnter += (_, _) =>
            {
                card.Background = new System.Windows.Media.SolidColorBrush(c(0x26, 0x26, 0x38));
                card.BorderBrush.BeginAnimation(System.Windows.Media.SolidColorBrush.ColorProperty, glowAnim);
            };
            card.MouseLeave += (_, _) =>
            {
                card.Background = new System.Windows.Media.SolidColorBrush(c(0x1e, 0x1e, 0x2e));
                card.BorderBrush.BeginAnimation(System.Windows.Media.SolidColorBrush.ColorProperty, unglowAnim);
            };
            card.MouseDown += (_, _) => OpenFtpSettings(ftp);

            return card;
        }

        private Border CreateToolCard(ToolItem tool, int index)
        {
            var c = System.Windows.Media.Color.FromRgb;

            var entranceTransform = new System.Windows.Media.TranslateTransform(0, 30);
            var entranceAnim = new System.Windows.Media.Animation.DoubleAnimation
            {
                From = 30, To = 0,
                Duration = TimeSpan.FromMilliseconds(400 + index * 60),
                EasingFunction = new System.Windows.Media.Animation.QuadraticEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
            };
            var fadeAnim = new System.Windows.Media.Animation.DoubleAnimation
            {
                From = 0, To = 0.95,
                Duration = TimeSpan.FromMilliseconds(400 + index * 60),
                EasingFunction = new System.Windows.Media.Animation.QuadraticEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
            };

            var avatar = new Border
            {
                Width = 44, Height = 44,
                CornerRadius = new System.Windows.CornerRadius(12),
                Background = new System.Windows.Media.SolidColorBrush(c(0x31, 0x32, 0x44)),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
                VerticalAlignment = System.Windows.VerticalAlignment.Top,
                Margin = new System.Windows.Thickness(20, 20, 0, 0),
                Child = new TextBlock
                {
                    Text = tool.Icon,
                    FontSize = 20,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    VerticalAlignment = System.Windows.VerticalAlignment.Center
                }
            };

            var nameText = new TextBlock
            {
                Text = tool.Name,
                FontSize = 17,
                FontWeight = FontWeights.SemiBold,
                Foreground = new System.Windows.Media.SolidColorBrush(c(0xcd, 0xd6, 0xf4)),
                Margin = new System.Windows.Thickness(72, 22, 20, 0),
                TextTrimming = TextTrimming.CharacterEllipsis,
                Height = 22,
                VerticalAlignment = System.Windows.VerticalAlignment.Top
            };

            var descText = new TextBlock
            {
                Text = tool.Description,
                FontSize = 10,
                Foreground = new System.Windows.Media.SolidColorBrush(c(0x6c, 0x70, 0x8b)),
                TextWrapping = TextWrapping.Wrap,
                Margin = new System.Windows.Thickness(72, 46, 20, 0),
                VerticalAlignment = System.Windows.VerticalAlignment.Top
            };

            var runBtn = new Button
            {
                Content = "▶ " + Localization.T("tools.run"),
                Height = 30,
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Background = new System.Windows.Media.SolidColorBrush(c(0x89, 0xb4, 0xfa)),
                Foreground = new System.Windows.Media.SolidColorBrush(c(0x1e, 0x1e, 0x2e)),
                BorderThickness = new System.Windows.Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand,
                Padding = new System.Windows.Thickness(14, 0, 14, 0),
                Margin = new System.Windows.Thickness(20, 0, 20, 14),
                VerticalAlignment = System.Windows.VerticalAlignment.Bottom,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch
            };
            runBtn.Click += (_, _) => RunTool(tool.Action);

            var innerGrid = new Grid();
            innerGrid.Children.Add(avatar);
            innerGrid.Children.Add(nameText);
            innerGrid.Children.Add(descText);
            innerGrid.Children.Add(runBtn);

            var card = new Border
            {
                Width = 250, Height = 220,
                Margin = new System.Windows.Thickness(8, 8, 28, 28),
                Background = new System.Windows.Media.SolidColorBrush(c(0x1e, 0x1e, 0x2e)),
                CornerRadius = new System.Windows.CornerRadius(14),
                BorderThickness = new System.Windows.Thickness(1),
                BorderBrush = new System.Windows.Media.SolidColorBrush(c(0x31, 0x32, 0x44)),
                Child = innerGrid,
                Opacity = 0
            };
            card.RenderTransform = entranceTransform;

            card.Loaded += (_, _) =>
            {
                entranceTransform.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, entranceAnim);
                card.BeginAnimation(System.Windows.UIElement.OpacityProperty, fadeAnim);
            };

            var glowAnim = new System.Windows.Media.Animation.ColorAnimation
            {
                To = c(0x89, 0xb4, 0xfa),
                Duration = TimeSpan.FromMilliseconds(150),
                EasingFunction = new System.Windows.Media.Animation.QuadraticEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
            };
            var unglowAnim = new System.Windows.Media.Animation.ColorAnimation
            {
                To = c(0x31, 0x32, 0x44),
                Duration = TimeSpan.FromMilliseconds(150),
                EasingFunction = new System.Windows.Media.Animation.QuadraticEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
            };
            card.MouseEnter += (_, _) =>
            {
                card.Background = new System.Windows.Media.SolidColorBrush(c(0x26, 0x26, 0x38));
                card.BorderBrush.BeginAnimation(System.Windows.Media.SolidColorBrush.ColorProperty, glowAnim);
            };
            card.MouseLeave += (_, _) =>
            {
                card.Background = new System.Windows.Media.SolidColorBrush(c(0x1e, 0x1e, 0x2e));
                card.BorderBrush.BeginAnimation(System.Windows.Media.SolidColorBrush.ColorProperty, unglowAnim);
            };

            return card;
        }

        private void OpenProject(Project project)
        {
            project.LastOpenedAt = DateTime.Now;
            ProjectService.Update(project);
            LastOpenedProject = project;
            new MainWindow().Show();
            Close();
        }

        private void OpenFtpSettings(FtpProject? ftp)
        {
            var wnd = new FtpSettingsWindow(ftp);
            wnd.Owner = this;
            if (wnd.ShowDialog() == true)
                LoadFtpProjects();
        }

        private void DeleteProject(Project project)
        {
            var deleteMsg = string.Format(Localization.T("lib.delete_msg"), project.Name);
            if (MessageBox.Show(this, deleteMsg,
                Localization.T("lib.delete_title"),
                MessageBoxButton.YesNo, MessageBoxImage.Warning)
                == MessageBoxResult.Yes)
            {
                ProjectService.Delete(project.Id);
                LoadProjects();
            }
        }

        private void DeleteFtpProject(FtpProject ftp)
        {
            var deleteMsg = string.Format(Localization.T("lib.delete_msg"), ftp.Name);
            if (MessageBox.Show(this, deleteMsg,
                Localization.T("lib.delete_title"),
                MessageBoxButton.YesNo, MessageBoxImage.Warning)
                == MessageBoxResult.Yes)
            {
                FtpProjectService.Delete(ftp.Id);
                LoadFtpProjects();
            }
        }

        private void NewProject_Click(object sender, RoutedEventArgs e)
        {
            var c = System.Windows.Media.Color.FromRgb;
            var dialog = new Window
            {
                Title = Localization.T("lib.new_title"), Width = 400,
                SizeToContent = SizeToContent.Height,
                ResizeMode = ResizeMode.NoResize,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = System.Windows.Media.Brushes.Transparent,
                FontFamily = new System.Windows.Media.FontFamily("Segoe UI")
            };

            var nameBox = new TextBox
            {
                FontSize = 14,
                Padding = new System.Windows.Thickness(12, 10, 12, 10),
                Background = new System.Windows.Media.SolidColorBrush(c(0x31, 0x32, 0x44)),
                Foreground = new System.Windows.Media.SolidColorBrush(c(0xcd, 0xd6, 0xf4)),
                BorderThickness = new System.Windows.Thickness(0),
                CaretBrush = new System.Windows.Media.SolidColorBrush(c(0x89, 0xb4, 0xfa)),
                VerticalAlignment = System.Windows.VerticalAlignment.Center
            };
            var genNameBtn = new Button
            {
                Content = "🎲",
                Width = 32, Height = 32,
                Background = new System.Windows.Media.SolidColorBrush(c(0x31, 0x32, 0x44)),
                Foreground = new System.Windows.Media.SolidColorBrush(c(0xcd, 0xd6, 0xf4)),
                BorderThickness = new System.Windows.Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand,
                FontSize = 14,
                Margin = new System.Windows.Thickness(6, 0, 0, 0),
                VerticalAlignment = System.Windows.VerticalAlignment.Center,
                ToolTip = Localization.T("lib.gen_tooltip")
            };
            genNameBtn.Click += (_, _) => { nameBox.Text = GeneratorService.GenerateName(); };
            var namePanel = new DockPanel();
            DockPanel.SetDock(genNameBtn, Dock.Right);
            namePanel.Children.Add(genNameBtn);
            namePanel.Children.Add(nameBox);

            var descText = new TextBlock
            {
                Text = Localization.T("lib.new_desc"),
                FontSize = 11,
                Foreground = new System.Windows.Media.SolidColorBrush(c(0x58, 0x5b, 0x70)),
                Margin = new System.Windows.Thickness(0, 4, 0, 0),
                TextWrapping = TextWrapping.Wrap
            };

            var okBtn = new Button
            {
                Content = Localization.T("lib.new_btn"), IsDefault = true, Height = 38,
                Background = new System.Windows.Media.SolidColorBrush(c(0x89, 0xb4, 0xfa)),
                Foreground = new System.Windows.Media.SolidColorBrush(c(0x1e, 0x1e, 0x2e)),
                BorderThickness = new System.Windows.Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand,
                FontSize = 14, FontWeight = FontWeights.Bold,
                Margin = new System.Windows.Thickness(0, 16, 0, 0)
            };
            var cancelBtn = new Button
            {
                Content = Localization.T("lib.new_cancel"), IsCancel = true, Height = 36,
                Background = System.Windows.Media.Brushes.Transparent,
                Foreground = new System.Windows.Media.SolidColorBrush(c(0x6c, 0x70, 0x8b)),
                BorderThickness = new System.Windows.Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand,
                FontSize = 13, Margin = new System.Windows.Thickness(0, 4, 0, 0)
            };

            var headerBar = new Border
            {
                Height = 4,
                Background = new System.Windows.Media.SolidColorBrush(c(0x89, 0xb4, 0xfa)),
                BorderThickness = new System.Windows.Thickness(0)
            };

            var fieldPanel = new StackPanel
                { Margin = new System.Windows.Thickness(28, 20, 28, 24) };
            fieldPanel.Children.Add(new TextBlock
            {
                Text = Localization.T("lib.new_name"), FontSize = 13, FontWeight = FontWeights.SemiBold,
                Foreground = new System.Windows.Media.SolidColorBrush(c(0xcd, 0xd6, 0xf4)),
                Margin = new System.Windows.Thickness(0, 0, 0, 6)
            });
            fieldPanel.Children.Add(namePanel);
            fieldPanel.Children.Add(descText);
            fieldPanel.Children.Add(okBtn);
            fieldPanel.Children.Add(cancelBtn);

            var outerStack = new StackPanel();
            outerStack.Children.Add(headerBar);
            outerStack.Children.Add(fieldPanel);

            dialog.Content = outerStack;
            dialog.Background = new System.Windows.Media.SolidColorBrush(c(0x18, 0x18, 0x25));

            Project? createdProject = null;
            okBtn.Click += (_, _) =>
            {
                var name = nameBox.Text.Trim();
                if (string.IsNullOrEmpty(name))
                {
                    MessageBox.Show(dialog, Localization.T("lib.new_name_err"), "",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                createdProject = new Project { Name = name };
                ProjectService.Create(createdProject);
                dialog.DialogResult = true;
                dialog.Close();
            };

            if (dialog.ShowDialog() == true && createdProject != null)
            {
                LoadProjects();
                OpenProject(createdProject);
            }
        }

        private void AddFtp_Click(object sender, RoutedEventArgs e)
        {
            OpenFtpSettings(null);
        }

        private void RunTool(string action)
        {
            switch (action)
            {
                case "steamcmd":
                    break;
                case "configconv":
                    break;
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void Window_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
                DragMove();
        }
    }
}
