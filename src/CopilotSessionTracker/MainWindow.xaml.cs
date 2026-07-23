using System;
using System.Collections.Generic;
using System.IO;
using CopilotSessionTracker.Models;
using CopilotSessionTracker.Services;
using CopilotSessionTracker.ViewModels;
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics;
using Color = Windows.UI.Color;

namespace CopilotSessionTracker;

public sealed partial class MainWindow : Window
{
    public MainViewModel ViewModel { get; } = new();

    public MainWindow()
    {
        this.InitializeComponent();
        Title = "Copilot Session Tracker";
        SetupWindow();

        // Kick off the initial load once the window is up.
        _ = ViewModel.RefreshAsync();
    }

    private void SetupWindow()
    {
        try
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = AppWindow.GetFromWindowId(windowId);
            if (appWindow is null)
            {
                return;
            }

            var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
            if (File.Exists(iconPath))
            {
                appWindow.SetIcon(iconPath);
            }

            if (Content is FrameworkElement root)
            {
                ApplyTitleBarTheme(appWindow.TitleBar, root.ActualTheme);
                root.ActualThemeChanged += (_, _) =>
                    ApplyTitleBarTheme(appWindow.TitleBar, root.ActualTheme);
            }

            // Size to 50% of the current display's work area (with sane minimums).
            var area = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Primary);
            var work = area?.WorkArea;

            var width = work is { } w ? Math.Max(900, w.Width / 2) : 1180;
            var height = work is { } h ? Math.Max(640, h.Height / 2) : 860;

            appWindow.Resize(new SizeInt32(width, height));

            // Center on that display.
            if (work is { } r)
            {
                var x = r.X + Math.Max(0, (r.Width - width) / 2);
                var y = r.Y + Math.Max(0, (r.Height - height) / 2);
                appWindow.Move(new PointInt32(x, y));
            }
        }
        catch
        {
            // Non-fatal: keep default size/position if the platform rejects the calls.
        }
    }

    private static void ApplyTitleBarTheme(AppWindowTitleBar titleBar, ElementTheme theme)
    {
        var dark = theme == ElementTheme.Dark;

        var background = dark
            ? Color.FromArgb(255, 32, 32, 32)
            : Color.FromArgb(255, 243, 243, 243);
        var foreground = dark
            ? Color.FromArgb(255, 249, 249, 249)
            : Color.FromArgb(255, 27, 27, 27);
        var inactiveForeground = dark
            ? Color.FromArgb(255, 160, 160, 160)
            : Color.FromArgb(255, 96, 96, 96);
        var hover = dark
            ? Color.FromArgb(255, 51, 51, 51)
            : Color.FromArgb(255, 229, 229, 229);
        var pressed = dark
            ? Color.FromArgb(255, 61, 61, 61)
            : Color.FromArgb(255, 216, 216, 216);

        titleBar.BackgroundColor = background;
        titleBar.ForegroundColor = foreground;
        titleBar.InactiveBackgroundColor = background;
        titleBar.InactiveForegroundColor = inactiveForeground;
        titleBar.ButtonBackgroundColor = background;
        titleBar.ButtonForegroundColor = foreground;
        titleBar.ButtonInactiveBackgroundColor = background;
        titleBar.ButtonInactiveForegroundColor = inactiveForeground;
        titleBar.ButtonHoverBackgroundColor = hover;
        titleBar.ButtonHoverForegroundColor = foreground;
        titleBar.ButtonPressedBackgroundColor = pressed;
        titleBar.ButtonPressedForegroundColor = foreground;
    }

    private void OpenTerminal_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: SessionInfo session })
        {
            return;
        }

        try
        {
            TerminalLauncher.OpenSession(session, ViewModel.CommandTemplate);
        }
        catch (Exception ex)
        {
            _ = ShowMessageAsync("Could not open terminal", ex.Message);
        }
    }

    private async void Settings_Click(object sender, RoutedEventArgs e)
    {
        var templateBox = new TextBox
        {
            FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas"),
            Text = ViewModel.CommandTemplate,
            AcceptsReturn = false,
            TextWrapping = TextWrapping.Wrap,
            PlaceholderText = TerminalLauncher.DefaultCommandTemplate,
        };
        AutomationProperties.SetName(templateBox, "Terminal command template");

        // AcceptsReturn must be true *before* Text is assigned. With the default
        // (false), WinUI keeps only the first line of a multi-line value, so reopening
        // Settings with several ignore roots looks like a single path.
        var ignoreBox = new TextBox
        {
            FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas"),
            AcceptsReturn = true,
            TextWrapping = TextWrapping.NoWrap,
            Height = 120,
            // WinUI multiline TextBox uses '\r' as its line separator; "\r\n" / '\n'
            // can still collapse visually even with AcceptsReturn on.
            Text = ToWinUiMultiline(ViewModel.IgnoredDirectoriesText),
        };
        ScrollViewer.SetVerticalScrollBarVisibility(ignoreBox, ScrollBarVisibility.Auto);
        AutomationProperties.SetName(ignoreBox, "Ignored working directories");

        var panel = new StackPanel { Spacing = 8, MinWidth = 520 };
        panel.Children.Add(new TextBlock
        {
            Text = "Command run by the Terminal button. Tokens: {id} = session id, "
                 + "{cwd} = working directory. Everything else is passed through verbatim "
                 + "(e.g. --yolo, --prefer-version <v>).",
            TextWrapping = TextWrapping.Wrap,
            Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
        });
        panel.Children.Add(templateBox);

        panel.Children.Add(new TextBlock
        {
            Text = "Ignored working directories (one path per line). Sessions whose working "
                 + "directory equals or lives under any of these are hidden from the list.",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 8, 0, 0),
            Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
        });
        panel.Children.Add(ignoreBox);

        var dialog = new ContentDialog
        {
            Title = "Settings",
            Content = panel,
            PrimaryButtonText = "Save",
            SecondaryButtonText = "Reset to default",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = Content.XamlRoot,
        };

        // "Reset to default" should repopulate the box without closing the dialog.
        dialog.SecondaryButtonClick += (_, args) =>
        {
            args.Cancel = true;
            templateBox.Text = TerminalLauncher.DefaultCommandTemplate;
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            var value = templateBox.Text?.Trim();
            ViewModel.CommandTemplate = string.IsNullOrEmpty(value)
                ? TerminalLauncher.DefaultCommandTemplate
                : value;

            ViewModel.UpdateIgnoredDirectories(ignoreBox.Text);
        }
    }

    private void CopyId_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string id } element || string.IsNullOrEmpty(id))
        {
            return;
        }

        var package = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
        package.SetText(id);
        Clipboard.SetContent(package);

        ShowCopiedFlyout(element);
    }

    private static void ShowCopiedFlyout(FrameworkElement target)
    {
        var flyout = new Flyout
        {
            Placement = FlyoutPlacementMode.Top,
            Content = new TextBlock { Text = "Copied!" },
        };
        flyout.ShowAt(target);

        // Auto-dismiss shortly after so it reads as transient feedback.
        var timer = target.DispatcherQueue.CreateTimer();
        timer.Interval = TimeSpan.FromMilliseconds(900);
        timer.IsRepeating = false;
        timer.Tick += (_, _) => flyout.Hide();
        timer.Start();
    }

    private async void Peek_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: SessionInfo session })
        {
            return;
        }

        IReadOnlyList<ConversationTurn> turns;
        try
        {
            turns = ViewModel.GetRecentTurns(session, 5);
        }
        catch (Exception ex)
        {
            await ShowMessageAsync("Could not read conversation", ex.Message);
            return;
        }

        var dialog = new ContentDialog
        {
            Title = session.Name,
            CloseButtonText = "Close",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = Content.XamlRoot,
            Content = BuildPeekContent(session, turns),
        };

        await dialog.ShowAsync();
    }

    private static FrameworkElement BuildPeekContent(
        SessionInfo session, IReadOnlyList<ConversationTurn> turns)
    {
        var outer = new StackPanel { Spacing = 12, MinWidth = 560 };

        outer.Children.Add(BuildMetadataPanel(session));

        outer.Children.Add(new TextBlock
        {
            Text = turns.Count == 0
                ? "No conversation turns recorded for this session."
                : $"Last {turns.Count} round trip(s):",
            FontWeight = FontWeights.SemiBold,
        });

        var turnsPanel = new StackPanel { Spacing = 10 };
        foreach (var turn in turns)
        {
            turnsPanel.Children.Add(BuildTurnCard(turn));
        }

        outer.Children.Add(new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            MaxHeight = 460,
            Content = turnsPanel,
        });

        return outer;
    }

    private static FrameworkElement BuildMetadataPanel(SessionInfo session)
    {
        var grid = new Grid { ColumnSpacing = 8, RowSpacing = 2 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        void AddRow(int row, string label, string value)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var labelBlock = new TextBlock
            {
                Text = label,
                FontWeight = FontWeights.SemiBold,
            };
            Grid.SetRow(labelBlock, row);
            Grid.SetColumn(labelBlock, 0);
            grid.Children.Add(labelBlock);

            var valueBlock = new TextBlock
            {
                Text = value,
                TextWrapping = TextWrapping.Wrap,
                IsTextSelectionEnabled = true,
            };
            Grid.SetRow(valueBlock, row);
            Grid.SetColumn(valueBlock, 1);
            grid.Children.Add(valueBlock);
        }

        AddRow(0, "Session id", session.Id);
        AddRow(1, "Directory", session.WorkingDirectoryDisplay);
        AddRow(2, "Created", session.CreatedDisplay);
        AddRow(3, "Last activity", session.LastActivityDisplay);
        AddRow(4, "Turns", session.TurnCount.ToString());

        return grid;
    }

    private static FrameworkElement BuildTurnCard(ConversationTurn turn)
    {
        var panel = new StackPanel { Spacing = 6 };

        panel.Children.Add(new TextBlock
        {
            Text = turn.Header,
            FontWeight = FontWeights.SemiBold,
            Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["AccentTextFillColorPrimaryBrush"],
        });

        panel.Children.Add(BuildMessageBlock("User", turn.UserDisplay));
        panel.Children.Add(BuildMessageBlock("Assistant", turn.AssistantDisplay));

        return new Border
        {
            Padding = new Thickness(12),
            CornerRadius = new CornerRadius(6),
            BorderThickness = new Thickness(1),
            BorderBrush = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
            Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
            Child = panel,
        };
    }

    private static FrameworkElement BuildMessageBlock(string role, string text)
    {
        var panel = new StackPanel { Spacing = 2 };
        panel.Children.Add(new TextBlock
        {
            Text = role,
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
        });
        panel.Children.Add(new TextBlock
        {
            Text = Truncate(text, 1200),
            TextWrapping = TextWrapping.Wrap,
            IsTextSelectionEnabled = true,
        });
        return panel;
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max] + "…";

    /// <summary>
    /// WinUI multiline <see cref="TextBox"/> uses <c>'\r'</c> as its line separator.
    /// Values joined with <see cref="Environment.NewLine"/> (<c>"\r\n"</c>) or bare
    /// <c>'\n'</c> can display as a single line even when <see cref="TextBox.AcceptsReturn"/>
    /// is true.
    /// </summary>
    private static string ToWinUiMultiline(string? text) =>
        (text ?? string.Empty).Replace("\r\n", "\r", StringComparison.Ordinal).Replace('\n', '\r');

    private async System.Threading.Tasks.Task ShowMessageAsync(string title, string message)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = message,
            CloseButtonText = "OK",
            XamlRoot = Content.XamlRoot,
        };
        await dialog.ShowAsync();
    }
}
