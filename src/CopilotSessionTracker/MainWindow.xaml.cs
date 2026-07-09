using System;
using System.Collections.Generic;
using CopilotSessionTracker.Models;
using CopilotSessionTracker.Services;
using CopilotSessionTracker.ViewModels;
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics;

namespace CopilotSessionTracker;

public sealed partial class MainWindow : Window
{
    public MainViewModel ViewModel { get; } = new();

    public MainWindow()
    {
        this.InitializeComponent();
        Title = "Copilot Session Tracker";
        SetupWindow(1180, 860);

        // Kick off the initial load once the window is up.
        _ = ViewModel.RefreshAsync();
    }

    private void SetupWindow(int width, int height)
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

            appWindow.Resize(new SizeInt32(width, height));

            // Center on the display that contains the window.
            var area = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Primary);
            if (area is not null)
            {
                var x = area.WorkArea.X + Math.Max(0, (area.WorkArea.Width - width) / 2);
                var y = area.WorkArea.Y + Math.Max(0, (area.WorkArea.Height - height) / 2);
                appWindow.Move(new PointInt32(x, y));
            }
        }
        catch
        {
            // Non-fatal: keep default size/position if the platform rejects the calls.
        }
    }

    private void OpenTerminal_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: SessionInfo session })
        {
            return;
        }

        try
        {
            TerminalLauncher.OpenSession(session);
        }
        catch (Exception ex)
        {
            _ = ShowMessageAsync("Could not open terminal", ex.Message);
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
