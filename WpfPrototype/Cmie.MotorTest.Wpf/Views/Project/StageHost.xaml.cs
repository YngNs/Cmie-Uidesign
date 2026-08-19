using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Cmie.MotorTest.Wpf.Dialogs;
using Cmie.MotorTest.Wpf.Models;
using Cmie.MotorTest.Wpf.Services;

namespace Cmie.MotorTest.Wpf.Views.Project;

public partial class StageHost : UserControl
{
    private readonly Dictionary<string, TestFloatingWindow> _windows = new();
    private readonly Dictionary<string, Rect> _restoredBounds = new();
    private int _zIndex = 20;
    private int _cascade;
    private string? _activeKey;

    public StageHost()
    {
        InitializeComponent();
    }

    public event Action<IReadOnlyCollection<string>, string?>? WindowStateChanged;
    public event Action<string>? StatusMessage;

    public void Open(TestItem item, TestProjectInfo project)
    {
        if (_windows.TryGetValue(item.Key, out var existing))
        {
            existing.Visibility = Visibility.Visible;
            FocusWindow(existing);
            StatusMessage?.Invoke($"已前置「{item.Title}」");
            return;
        }

        var window = new TestFloatingWindow(item, project)
        {
            Width = 820,
            Height = 520
        };
        window.FocusRequested += FocusWindow;
        window.MinimizeRequested += MinimizeWindow;
        window.CloseRequested += CloseWindow;
        window.MaximizeRequested += ToggleMaximizeWindow;
        window.ToolInvoked += (source, action) => _ = HandleToolActionAsync(source, action);

        var offset = 22 * (_cascade++ % 6);
        Canvas.SetLeft(window, 16 + offset);
        Canvas.SetTop(window, 16 + offset);
        FloatCanvas.Children.Add(window);
        _windows[item.Key] = window;
        EmptyHint.Visibility = Visibility.Collapsed;
        FocusWindow(window);
        Dispatcher.BeginInvoke(
            DispatcherPriority.Loaded,
            new Action(() => LayoutNewWindow(window, offset)));
        StatusMessage?.Invoke($"已打开「{item.Title}」· 可从任意边缘调整大小");
    }

    private async Task HandleToolActionAsync(TestFloatingWindow window, string action)
    {
        try
        {
            if (action is "表格清空" or "删除记录" or "数据保存" or "删除粗差")
            {
                UserSession.EnsureCanModifyTests();
            }
            else if (action is "读取数据" or "数据调用" or "原始记录" or "结果计算" or "阻值计算" or "数据分析")
            {
                UserSession.EnsureSignedIn();
            }

            if (action is "表格清空")
            {
                window.ClearWorksheet();
                StatusMessage?.Invoke("已清空当前工作表；尚未覆盖本地数据");
                return;
            }

            if (action is "删除记录")
            {
                await ShowHistoryAsync(window, deleteRequested: true);
                return;
            }

            if (action is "数据保存")
            {
                var record = await LocalDataService.Current.SaveWorksheetRecordAsync(window.CaptureWorksheet());
                StatusMessage?.Invoke($"已创建历史记录 · {record.UpdatedAt:yyyy-MM-dd HH:mm:ss}");
                return;
            }

            if (action is "读取数据" or "数据调用" or "原始记录")
            {
                await ShowHistoryAsync(window, deleteRequested: false);
                return;
            }

            if (action is "结果计算" or "阻值计算" or "数据分析")
            {
                if (action == "阻值计算" && window.TestKey == "resistance")
                {
                    StatusMessage?.Invoke(window.CalculateResistance());
                    return;
                }

                if (action == "结果计算" && window.TestKey == "noload")
                {
                    StatusMessage?.Invoke(window.CalculateNoLoad());
                    if (window.LastNoLoadResult is { } noLoad)
                        await LocalDataService.Current.SaveNoLoadAnalysisAsync(window.ProjectId, noLoad);
                    return;
                }

                if (action == "数据分析" && window.TestKey == "method-a")
                {
                    var noLoad = await LocalDataService.Current.LoadNoLoadAnalysisAsync(window.ProjectId)
                        ?? throw new InvalidOperationException("请先完成同一项目的空载试验结果计算，以获得机械损耗和铁耗曲线。 ");
                    StatusMessage?.Invoke(window.CalculateMethodA(noLoad));
                    return;
                }

                var result = await LocalDataService.Current.CalculateAsync(window.CaptureWorksheet());
                StatusMessage?.Invoke(result.NumericCount == 0
                    ? "工作表中没有可计算的数值"
                    : $"本地计算完成 · {result.NumericCount} 个数值 · 均值 {result.Average:F3} · 不平衡度 {result.UnbalancePercent:F2}%");
                return;
            }

            if (action is "参数导入" or "冷态温度")
            {
                StatusMessage?.Invoke($"已使用当前项目参数：{window.ProjectId}");
                return;
            }

            StatusMessage?.Invoke($"试验操作：{action}");
        }
        catch (Exception exception)
        {
            StatusMessage?.Invoke(LocalDataService.FriendlyError(exception));
        }
    }

    private async Task ShowHistoryAsync(TestFloatingWindow window, bool deleteRequested)
    {
        var records = await LocalDataService.Current.ListWorksheetRecordsAsync(window.ProjectId, window.TestKey);
        if (records.Count == 0)
        {
            var legacy = await LocalDataService.Current.LoadWorksheetAsync(window.ProjectId, window.TestKey);
            if (legacy is not null && !deleteRequested)
            {
                window.ApplyWorksheet(legacy);
                StatusMessage?.Invoke($"已读取旧版最新记录 · {legacy.UpdatedAt:yyyy-MM-dd HH:mm:ss}");
                return;
            }
            StatusMessage?.Invoke("本地尚无该试验的历史记录");
            return;
        }

        var dialog = new WorksheetHistoryDialog(records)
        {
            Owner = Window.GetWindow(this)
        };
        if (deleteRequested) dialog.Title = "选择要删除的历史记录";
        if (dialog.ShowDialog() != true || dialog.SelectedRecord is not { } selected) return;

        if (dialog.SelectedAction == "delete" || deleteRequested)
        {
            UserSession.EnsureCanModifyTests();
            await LocalDataService.Current.DeleteWorksheetRecordAsync(window.ProjectId, window.TestKey, selected.RecordId);
            StatusMessage?.Invoke($"已删除历史记录 · {selected.Name}");
            return;
        }

        var record = await LocalDataService.Current.LoadWorksheetRecordAsync(window.ProjectId, window.TestKey, selected.RecordId);
        if (record is null)
        {
            StatusMessage?.Invoke("所选记录已不存在，请刷新后重试");
            return;
        }
        window.ApplyWorksheet(record.Worksheet);
        StatusMessage?.Invoke($"已读取 · {record.Name} · 保存人 {record.SavedBy}");
    }

    private void FocusWindow(TestFloatingWindow window)
    {
        window.Visibility = Visibility.Visible;
        _activeKey = window.TestKey;
        Panel.SetZIndex(window, ++_zIndex);

        foreach (var candidate in _windows.Values)
        {
            candidate.Opacity = ReferenceEquals(candidate, window) ? 1 : 0.94;
        }

        RebuildTaskbar();
        RaiseWindowStateChanged();
    }

    private void MinimizeWindow(TestFloatingWindow window)
    {
        window.Visibility = Visibility.Collapsed;
        _activeKey = _windows.Values
            .Where(candidate => !ReferenceEquals(candidate, window) && candidate.Visibility == Visibility.Visible)
            .OrderBy(Panel.GetZIndex)
            .LastOrDefault()?.TestKey;

        if (_activeKey is not null)
        {
            FocusWindow(_windows[_activeKey]);
            return;
        }

        RebuildTaskbar();
        RaiseWindowStateChanged();
    }

    private void CloseWindow(TestFloatingWindow window)
    {
        FloatCanvas.Children.Remove(window);
        _windows.Remove(window.TestKey);
        _restoredBounds.Remove(window.TestKey);

        var next = _windows.Values
            .Where(candidate => candidate.Visibility == Visibility.Visible)
            .OrderBy(Panel.GetZIndex)
            .LastOrDefault();

        if (next is not null)
        {
            FocusWindow(next);
        }
        else
        {
            _activeKey = null;
            EmptyHint.Visibility = _windows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            RebuildTaskbar();
            RaiseWindowStateChanged();
        }

        StatusMessage?.Invoke(_windows.Count == 0 ? "已关闭全部试验浮窗" : $"已关闭「{window.TestTitle}」");
    }

    private void RebuildTaskbar()
    {
        TaskbarPanel.Children.Clear();
        TaskbarRoot.Visibility = _windows.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        HeaderHint.Visibility = _windows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        foreach (var window in _windows.Values)
        {
            var isMinimized = window.Visibility != Visibility.Visible;
            var button = new Button
            {
                Content = $"{(isMinimized ? "○" : "●")}  {window.TestTitle}",
                Height = 26,
                Margin = new Thickness(0, 0, 5, 0),
                Padding = new Thickness(8, 0, 8, 0),
                FontSize = 10.5,
                Foreground = (Brush)FindResource(window.TestKey == _activeKey ? "AccentBrush" : "TextBrush"),
                Style = (Style)FindResource("ActionButtonStyle"),
                ToolTip = isMinimized ? "还原窗口" : "前置窗口"
            };
            button.Click += (_, _) => FocusWindow(window);
            TaskbarPanel.Children.Add(button);
        }
    }

    private void RaiseWindowStateChanged() =>
        WindowStateChanged?.Invoke(_windows.Keys.ToArray(), _activeKey);

    private void LayoutNewWindow(TestFloatingWindow window, double offset)
    {
        if (!_windows.ContainsKey(window.TestKey)
            || FloatCanvas.ActualWidth <= 0
            || FloatCanvas.ActualHeight <= 0)
        {
            return;
        }

        var maxWidth = Math.Max(window.MinWidth, FloatCanvas.ActualWidth - 32);
        var maxHeight = Math.Max(window.MinHeight, FloatCanvas.ActualHeight - 32);
        window.Width = Math.Min(maxWidth, Math.Max(window.MinWidth, FloatCanvas.ActualWidth * 0.88));
        window.Height = Math.Min(maxHeight, Math.Max(window.MinHeight, FloatCanvas.ActualHeight * 0.90));

        var centeredLeft = (FloatCanvas.ActualWidth - window.Width) / 2 + offset;
        var centeredTop = (FloatCanvas.ActualHeight - window.Height) / 2 + offset;
        Canvas.SetLeft(window, Math.Clamp(centeredLeft, 12, Math.Max(12, FloatCanvas.ActualWidth - window.Width - 12)));
        Canvas.SetTop(window, Math.Clamp(centeredTop, 12, Math.Max(12, FloatCanvas.ActualHeight - window.Height - 12)));
    }

    private void ToggleMaximizeWindow(TestFloatingWindow window)
    {
        if (window.IsMaximized)
        {
            if (_restoredBounds.Remove(window.TestKey, out var bounds))
            {
                Canvas.SetLeft(window, bounds.Left);
                Canvas.SetTop(window, bounds.Top);
                window.Width = bounds.Width;
                window.Height = bounds.Height;
            }

            window.SetMaximized(false);
            return;
        }

        _restoredBounds[window.TestKey] = new Rect(
            Canvas.GetLeft(window),
            Canvas.GetTop(window),
            window.ActualWidth,
            window.ActualHeight);
        window.SetMaximized(true);
        ApplyMaximizedBounds(window);
        FocusWindow(window);
    }

    private void ApplyMaximizedBounds(TestFloatingWindow window)
    {
        const double margin = 4;
        Canvas.SetLeft(window, margin);
        Canvas.SetTop(window, margin);
        window.Width = Math.Max(window.MinWidth, FloatCanvas.ActualWidth - margin * 2);
        window.Height = Math.Max(window.MinHeight, FloatCanvas.ActualHeight - margin * 2);
    }

    private void FloatCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        foreach (var window in _windows.Values)
        {
            if (window.IsMaximized)
            {
                ApplyMaximizedBounds(window);
            }
            else
            {
                FitWindowInsideCanvas(window);
            }
        }
    }

    private void FitWindowInsideCanvas(TestFloatingWindow window)
    {
        const double margin = 8;
        var maxWidth = Math.Max(window.MinWidth, FloatCanvas.ActualWidth - margin * 2);
        var maxHeight = Math.Max(window.MinHeight, FloatCanvas.ActualHeight - margin * 2);
        window.Width = Math.Min(window.ActualWidth > 0 ? window.ActualWidth : window.Width, maxWidth);
        window.Height = Math.Min(window.ActualHeight > 0 ? window.ActualHeight : window.Height, maxHeight);

        var left = Canvas.GetLeft(window);
        var top = Canvas.GetTop(window);
        Canvas.SetLeft(window, Math.Clamp(double.IsNaN(left) ? margin : left, margin, Math.Max(margin, FloatCanvas.ActualWidth - window.Width - margin)));
        Canvas.SetTop(window, Math.Clamp(double.IsNaN(top) ? margin : top, margin, Math.Max(margin, FloatCanvas.ActualHeight - window.Height - margin)));
    }
}
