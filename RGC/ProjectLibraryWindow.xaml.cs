using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using RGC.Models;
using RGC.Services;

namespace RGC
{
    public partial class ProjectLibraryWindow : Window
    {
        public static Project? LastOpenedProject { get; private set; }
        private static readonly System.Windows.Media.Color[] AccentColors = new[]
        {
            System.Windows.Media.Color.FromRgb(0x89, 0xb4, 0xfa), // blue
            System.Windows.Media.Color.FromRgb(0xa6, 0xe3, 0xa1), // green
            System.Windows.Media.Color.FromRgb(0xf5, 0xc2, 0xe7), // pink
            System.Windows.Media.Color.FromRgb(0xfa, 0xe8, 0xb0), // yellow
            System.Windows.Media.Color.FromRgb(0xba, 0xc7, 0xfa), // lavender
            System.Windows.Media.Color.FromRgb(0x94, 0xe2, 0xd5), // teal
        };

        public ProjectLibraryWindow()
        {
            InitializeComponent();
            ApplyLang();
            LoadProjects();
        }

        private void ApplyLang()
        {
            LibTitle.Text = RGC.Services.Localization.T("lib.title");
            NewProjectBtn.Content = RGC.Services.Localization.T("lib.new");
            EmptyTitle.Text = RGC.Services.Localization.T("lib.empty_title");
            EmptyDesc.Text = RGC.Services.Localization.T("lib.empty_desc");
            EmptyBtn.Content = RGC.Services.Localization.T("lib.empty_btn");
        }

        private void LoadProjects()
        {
            ProjectTilesPanel.Children.Clear();
            var projects = ProjectService.LoadAll();
            projects = projects.OrderByDescending(p => p.LastOpenedAt).ToList();

            var cnt = projects.Count;
            var isEn = SettingsService.Language == "EN";
            var cntStr = isEn ? (cnt == 1 ? "project" : "projects")
                              : (cnt == 1 ? "проект" : (cnt >= 2 && cnt <= 4 ? "проекта" : "проектов"));
            ProjectCountText.Text = $"{cnt} {cntStr}";
            EmptyState.Visibility = projects.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            ScrollArea.Visibility = projects.Count == 0 ? Visibility.Collapsed : Visibility.Visible;

            for (var i = 0; i < projects.Count; i++)
                ProjectTilesPanel.Children.Add(CreateCard(projects[i], i));
        }

        private System.Windows.Controls.Border CreateCard(Project project, int index)
        {
            var c = System.Windows.Media.Color.FromRgb;
            var accent = AccentColors[index % AccentColors.Length];

            var serverOk = !string.IsNullOrEmpty(project.ServerPath)
                && System.IO.Directory.Exists(project.ServerPath);

            // Get DayZ version
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

            // === ENTRANCE ANIMATION: fade in + slide up ===
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

            // Avatar icon
            var avatar = new System.Windows.Controls.Border
            {
                Width = 44, Height = 44,
                CornerRadius = new System.Windows.CornerRadius(12),
                Background = new System.Windows.Media.SolidColorBrush(c(0x31, 0x32, 0x44)),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
                VerticalAlignment = System.Windows.VerticalAlignment.Top,
                Margin = new System.Windows.Thickness(20, 20, 0, 0),
                Child = new System.Windows.Controls.Image
                {
                    Source = new System.Windows.Media.Imaging.BitmapImage(
                        new System.Uri("pack://application:,,,/project_icon.png")),
                    Width = 24, Height = 24,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    VerticalAlignment = System.Windows.VerticalAlignment.Center
                }
            };

            // Project name
            var nameText = new System.Windows.Controls.TextBlock
            {
                Text = project.Name,
                FontSize = 17,
                FontWeight = System.Windows.FontWeights.SemiBold,
                Foreground = new System.Windows.Media.SolidColorBrush(c(0xcd, 0xd6, 0xf4)),
                Margin = new System.Windows.Thickness(72, 22, 20, 0),
                TextTrimming = System.Windows.TextTrimming.CharacterEllipsis,
                Height = 22,
                VerticalAlignment = System.Windows.VerticalAlignment.Top
            };

            // Status text
            var statusText = new System.Windows.Controls.TextBlock
            {
                Text = serverOk ? RGC.Services.Localization.T("lib.configured") : RGC.Services.Localization.T("lib.not_configured"),
                FontSize = 10,
                Foreground = new System.Windows.Media.SolidColorBrush(serverOk ? c(0xa6, 0xe3, 0xa1) : c(0x6c, 0x70, 0x8b)),
                TextTrimming = System.Windows.TextTrimming.CharacterEllipsis
            };

            // Full server path (small, truncated)
            var pathFull = serverOk ? project.ServerPath : RGC.Services.Localization.T("lib.no_path");
            var pathText = new System.Windows.Controls.TextBlock
            {
                Text = pathFull,
                FontSize = 9,
                Foreground = new System.Windows.Media.SolidColorBrush(c(0x58, 0x5b, 0x70)),
                TextTrimming = System.Windows.TextTrimming.CharacterEllipsis,
                ToolTip = pathFull
            };

            // Version + last opened on one line
            var infoText = new System.Windows.Controls.TextBlock
            {
                FontSize = 9,
                Foreground = new System.Windows.Media.SolidColorBrush(c(0x58, 0x5b, 0x70)),
                TextTrimming = System.Windows.TextTrimming.CharacterEllipsis
            };
            var lastOpen = string.Format(RGC.Services.Localization.T("lib.last_open"), project.LastOpenedAt.ToString("dd MMM yyyy"));
            if (!string.IsNullOrEmpty(dayzVersion))
                infoText.Text = $"{string.Format(RGC.Services.Localization.T("lib.dayz_version"), dayzVersion)} · {lastOpen}";
            else
                infoText.Text = lastOpen;

            // Open button
            var openBtn = new System.Windows.Controls.Button
            {
                Content = RGC.Services.Localization.T("lib.open"),
                Height = 30,
                FontSize = 11,
                FontWeight = System.Windows.FontWeights.SemiBold,
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

            // Delete button
            var deleteBtn = new System.Windows.Controls.Button
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

            // Status dot
            var statusDot = new System.Windows.Controls.Border
            {
                Width = 8, Height = 8,
                CornerRadius = new System.Windows.CornerRadius(4),
                Background = new System.Windows.Media.SolidColorBrush(serverOk ? c(0xa6, 0xe3, 0xa1) : c(0x6c, 0x70, 0x8b)),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
                VerticalAlignment = System.Windows.VerticalAlignment.Top,
                Margin = new System.Windows.Thickness(0, 14, 46, 0)
            };

            // Stack info in top area
            var infoStack = new System.Windows.Controls.StackPanel
            {
                Margin = new System.Windows.Thickness(72, 46, 20, 0),
                VerticalAlignment = System.Windows.VerticalAlignment.Top
            };
            infoStack.Children.Add(statusText);
            infoStack.Children.Add(pathText);
            infoStack.Children.Add(infoText);

            // Layout
            var innerGrid = new System.Windows.Controls.Grid();
            innerGrid.Children.Add(avatar);
            innerGrid.Children.Add(nameText);
            innerGrid.Children.Add(infoStack);
            innerGrid.Children.Add(openBtn);
            innerGrid.Children.Add(deleteBtn);
            innerGrid.Children.Add(statusDot);

            var card = new System.Windows.Controls.Border
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

            // === Entrance animation ===
            card.Loaded += (_, _) =>
            {
                entranceTransform.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, entranceAnim);
                card.BeginAnimation(System.Windows.UIElement.OpacityProperty, fadeAnim);
            };

            // === Hover animation ===
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

        private void OpenProject(Project project)
        {
            project.LastOpenedAt = DateTime.Now;
            ProjectService.Update(project);
            LastOpenedProject = project;
            new MainWindow().Show();
            Close();
        }

        private void DeleteProject(Project project)
        {
            var deleteMsg = string.Format(RGC.Services.Localization.T("lib.delete_msg"), project.Name);
            if (System.Windows.MessageBox.Show(this, deleteMsg,
                RGC.Services.Localization.T("lib.delete_title"),
                System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning)
                == System.Windows.MessageBoxResult.Yes)
            {
                ProjectService.Delete(project.Id);
                LoadProjects();
            }
        }

        private void NewProject_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            var c = System.Windows.Media.Color.FromRgb;
            var dialog = new System.Windows.Window
            {
                Title = "Новый проект", Width = 400,
                SizeToContent = System.Windows.SizeToContent.Height,
                ResizeMode = System.Windows.ResizeMode.NoResize,
                WindowStartupLocation = System.Windows.WindowStartupLocation.CenterOwner,
                Owner = this,
                WindowStyle = System.Windows.WindowStyle.None,
                AllowsTransparency = true,
                Background = System.Windows.Media.Brushes.Transparent,
                FontFamily = new System.Windows.Media.FontFamily("Segoe UI")
            };

            var nameBox = new System.Windows.Controls.TextBox
            {
                FontSize = 14,
                Padding = new System.Windows.Thickness(12, 10, 12, 10),
                Background = new System.Windows.Media.SolidColorBrush(c(0x31, 0x32, 0x44)),
                Foreground = new System.Windows.Media.SolidColorBrush(c(0xcd, 0xd6, 0xf4)),
                BorderThickness = new System.Windows.Thickness(0),
                CaretBrush = new System.Windows.Media.SolidColorBrush(c(0x89, 0xb4, 0xfa)),
                VerticalAlignment = System.Windows.VerticalAlignment.Center
            };
            var genNameBtn = new System.Windows.Controls.Button
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
                ToolTip = "Сгенерировать название"
            };
            genNameBtn.Click += (_, _) => { nameBox.Text = GeneratorService.GenerateName(); };
            var namePanel = new System.Windows.Controls.DockPanel();
            System.Windows.Controls.DockPanel.SetDock(genNameBtn, System.Windows.Controls.Dock.Right);
            namePanel.Children.Add(genNameBtn);
            namePanel.Children.Add(nameBox);

            var descText = new System.Windows.Controls.TextBlock
            {
                Text = "Папку сервера можно будет указать позже в настройках проекта.",
                FontSize = 11,
                Foreground = new System.Windows.Media.SolidColorBrush(c(0x58, 0x5b, 0x70)),
                Margin = new System.Windows.Thickness(0, 4, 0, 0),
                TextWrapping = System.Windows.TextWrapping.Wrap
            };

            var okBtn = new System.Windows.Controls.Button
            {
                Content = RGC.Services.Localization.T("lib.new_btn"), IsDefault = true, Height = 38,
                Background = new System.Windows.Media.SolidColorBrush(c(0x89, 0xb4, 0xfa)),
                Foreground = new System.Windows.Media.SolidColorBrush(c(0x1e, 0x1e, 0x2e)),
                BorderThickness = new System.Windows.Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand,
                FontSize = 14, FontWeight = System.Windows.FontWeights.Bold,
                Margin = new System.Windows.Thickness(0, 16, 0, 0)
            };
            var cancelBtn = new System.Windows.Controls.Button
            {
                Content = RGC.Services.Localization.T("lib.new_cancel"), IsCancel = true, Height = 36,
                Background = System.Windows.Media.Brushes.Transparent,
                Foreground = new System.Windows.Media.SolidColorBrush(c(0x6c, 0x70, 0x8b)),
                BorderThickness = new System.Windows.Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand,
                FontSize = 13, Margin = new System.Windows.Thickness(0, 4, 0, 0)
            };

            var headerBar = new System.Windows.Controls.Border
            {
                Height = 4,
                Background = new System.Windows.Media.SolidColorBrush(c(0x89, 0xb4, 0xfa)),
                BorderThickness = new System.Windows.Thickness(0)
            };

            var fieldPanel = new System.Windows.Controls.StackPanel
                { Margin = new System.Windows.Thickness(28, 20, 28, 24) };
            fieldPanel.Children.Add(new System.Windows.Controls.TextBlock
            {
                Text = RGC.Services.Localization.T("lib.new_name"), FontSize = 13, FontWeight = System.Windows.FontWeights.SemiBold,
                Foreground = new System.Windows.Media.SolidColorBrush(c(0xcd, 0xd6, 0xf4)),
                Margin = new System.Windows.Thickness(0, 0, 0, 6)
            });
            fieldPanel.Children.Add(namePanel);
            fieldPanel.Children.Add(descText);
            fieldPanel.Children.Add(okBtn);
            fieldPanel.Children.Add(cancelBtn);

            var outerStack = new System.Windows.Controls.StackPanel();
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
                    System.Windows.MessageBox.Show(dialog, "Введите название проекта.", "Ошибка",
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
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

        private void Close_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            System.Windows.Application.Current.Shutdown();
        }

        private void Window_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
                DragMove();
        }
    }
}
