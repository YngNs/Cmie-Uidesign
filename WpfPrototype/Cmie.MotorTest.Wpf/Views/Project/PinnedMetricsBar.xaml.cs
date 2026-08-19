using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Cmie.MotorTest.Wpf.Models;

namespace Cmie.MotorTest.Wpf.Views.Project;

public partial class PinnedMetricsBar : UserControl
{
    private const double MetricCardStride = 196;
    private static readonly Duration ReorderDuration = TimeSpan.FromMilliseconds(140);
    private readonly ObservableCollection<MetricReading> _overflowItems = [];
    private bool _isDragging;
    private int _visibleCount;
    private Point _dragStart;
    private MetricReading? _dragCandidate;
    private Border? _dragSourceCard;
    private Border? _dropTargetCard;
    private ObservableCollection<MetricReading>? _items;

    public PinnedMetricsBar()
    {
        InitializeComponent();
    }

    public ObservableCollection<MetricReading>? Items
    {
        get => _items;
        set
        {
            if (_items is not null)
            {
                _items.CollectionChanged -= Items_CollectionChanged;
            }

            _items = value;
            ItemsHost.ItemsSource = value;
            PopupItemsHost.ItemsSource = _overflowItems;
            if (_items is not null)
            {
                _items.CollectionChanged += Items_CollectionChanged;
            }

            RefreshVisibility();
        }
    }

    public event Action<MetricReading>? RemoveRequested;
    public event Action? ClearRequested;

    private void Items_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RefreshVisibility();
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, UpdateOverflow);
    }

    public void RefreshVisibility()
    {
        var count = Items?.Count ?? 0;
        Root.Visibility = count == 0 ? Visibility.Collapsed : Visibility.Visible;
        HeaderCountText.Text = $"{count} 项";
        PopupCountText.Text = $"{count} 项";
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, UpdateOverflow);
    }

    private void MetricCard_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is not Border { Tag: MetricReading metric } card
            || FindAncestor<Button>(e.OriginalSource as DependencyObject) is not null)
        {
            _dragCandidate = null;
            _dragSourceCard = null;
            return;
        }

        _dragStart = e.GetPosition(card);
        _dragCandidate = metric;
        _dragSourceCard = card;
    }

    private void MetricCard_PreviewMouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        _dragCandidate = null;
        _dragSourceCard = null;
    }

    private void MetricCard_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_dragCandidate is null
            || _dragSourceCard is null
            || e.LeftButton != System.Windows.Input.MouseButtonState.Pressed)
        {
            return;
        }

        var current = e.GetPosition(_dragSourceCard);
        if (Math.Abs(current.X - _dragStart.X) < SystemParameters.MinimumHorizontalDragDistance
            && Math.Abs(current.Y - _dragStart.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        var metric = _dragCandidate;
        var sourceCard = _dragSourceCard;
        _dragCandidate = null;
        sourceCard.Opacity = 0.45;
        _isDragging = true;

        try
        {
            System.Windows.DragDrop.DoDragDrop(sourceCard, metric, DragDropEffects.Move);
        }
        finally
        {
            _isDragging = false;
            sourceCard.Opacity = 1;
            ClearDropTarget();
            _dragSourceCard = null;
            UpdateOverflow();
        }
    }

    private void MetricCard_GiveFeedback(object sender, GiveFeedbackEventArgs e)
    {
        e.UseDefaultCursors = false;
        Mouse.SetCursor(Cursors.Arrow);
        e.Handled = true;
    }

    private void MetricCard_DragEnter(object sender, DragEventArgs e) => UpdateDropTarget(sender, e);

    private void MetricCard_DragOver(object sender, DragEventArgs e)
    {
        UpdateDropTarget(sender, e);
        ReorderDuringDrag(sender, e);
    }

    private void MetricCard_DragLeave(object sender, DragEventArgs e)
    {
        if (ReferenceEquals(sender, _dropTargetCard))
        {
            ClearDropTarget();
        }
    }

    private void MetricCard_Drop(object sender, DragEventArgs e)
    {
        e.Effects = DragDropEffects.Move;
        e.Handled = true;
        ClearDropTarget();
    }

    private void ReorderDuringDrag(object sender, DragEventArgs e)
    {
        if (Items is null
            || sender is not Border { Tag: MetricReading targetMetric } targetCard
            || e.Data.GetData(typeof(MetricReading)) is not MetricReading draggedMetric
            || ReferenceEquals(draggedMetric, targetMetric))
        {
            return;
        }

        var isVertical = targetCard.Name == "OverflowCard";
        var source = isVertical ? _overflowItems : Items;
        var from = source.IndexOf(draggedMetric);
        var targetIndex = source.IndexOf(targetMetric);
        if (from < 0 || targetIndex < 0)
        {
            return;
        }

        var pointer = e.GetPosition(targetCard);
        var afterTarget = isVertical
            ? pointer.Y > targetCard.ActualHeight / 2
            : pointer.X > targetCard.ActualWidth / 2;
        var insertIndex = afterTarget ? targetIndex + 1 : targetIndex;
        if (insertIndex > from)
        {
            insertIndex--;
        }

        var to = Math.Clamp(insertIndex, 0, source.Count - 1);
        if (from == to)
        {
            return;
        }

        var host = isVertical ? PopupItemsHost : ItemsHost;
        var previousPositions = CapturePositions(host, source, from, to, isVertical);

        if (isVertical)
        {
            _overflowItems.Move(from, to);
            var originalFrom = Items.IndexOf(draggedMetric);
            var originalTo = Math.Clamp(_visibleCount + to, 0, Items.Count - 1);
            if (originalFrom >= 0 && originalFrom != originalTo)
            {
                Items.Move(originalFrom, originalTo);
            }
        }
        else
        {
            Items.Move(from, to);
        }

        ClearDropTarget();
        Dispatcher.BeginInvoke(
            DispatcherPriority.Loaded,
            new Action(() => AnimateReorder(host, previousPositions, isVertical)));
    }

    private void UpdateDropTarget(object sender, DragEventArgs e)
    {
        if (sender is not Border card || !e.Data.GetDataPresent(typeof(MetricReading)))
        {
            e.Effects = DragDropEffects.None;
            return;
        }

        if (!ReferenceEquals(card, _dropTargetCard))
        {
            ClearDropTarget();
            _dropTargetCard = card;
            card.SetResourceReference(Border.BorderBrushProperty, "AccentBrush");
            card.BorderThickness = new Thickness(1.5);
        }

        e.Effects = DragDropEffects.Move;
        e.Handled = true;
    }

    private void ClearDropTarget()
    {
        if (_dropTargetCard is null)
        {
            return;
        }

        _dropTargetCard.SetResourceReference(Border.BorderBrushProperty, "LineSoftBrush");
        _dropTargetCard.BorderThickness = new Thickness(1);
        _dropTargetCard = null;
    }

    private static T? FindAncestor<T>(DependencyObject? current)
        where T : DependencyObject
    {
        while (current is not null)
        {
            if (current is T match)
            {
                return match;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    private static Dictionary<MetricReading, double> CapturePositions(
        ItemsControl host,
        IReadOnlyList<MetricReading> source,
        int from,
        int to,
        bool vertical)
    {
        var positions = new Dictionary<MetricReading, double>();
        var first = Math.Min(from, to);
        var last = Math.Max(from, to);
        for (var index = first; index <= last; index++)
        {
            var metric = source[index];
            if (host.ItemContainerGenerator.ContainerFromItem(metric) is FrameworkElement container)
            {
                var point = container.TranslatePoint(new Point(0, 0), host);
                positions[metric] = vertical ? point.Y : point.X;
            }
        }

        return positions;
    }

    private static void AnimateReorder(
        ItemsControl host,
        IReadOnlyDictionary<MetricReading, double> previousPositions,
        bool vertical)
    {
        foreach (var (metric, previousPosition) in previousPositions)
        {
            if (host.ItemContainerGenerator.ContainerFromItem(metric) is not FrameworkElement container)
            {
                continue;
            }

            var point = container.TranslatePoint(new Point(0, 0), host);
            var currentPosition = vertical ? point.Y : point.X;
            var offset = previousPosition - currentPosition;
            if (Math.Abs(offset) < 0.5)
            {
                continue;
            }

            var transform = vertical
                ? new TranslateTransform(0, offset)
                : new TranslateTransform(offset, 0);
            container.RenderTransform = transform;

            var animation = new DoubleAnimation(offset, 0, ReorderDuration)
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
                FillBehavior = FillBehavior.Stop
            };
            animation.Completed += (_, _) =>
            {
                var property = vertical ? TranslateTransform.YProperty : TranslateTransform.XProperty;
                transform.BeginAnimation(property, null);
                container.RenderTransform = null;
            };
            var targetProperty = vertical ? TranslateTransform.YProperty : TranslateTransform.XProperty;
            transform.BeginAnimation(targetProperty, animation);
        }
    }

    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: MetricReading metric })
        {
            RemoveRequested?.Invoke(metric);
        }
    }

    private void Root_SizeChanged(object sender, SizeChangedEventArgs e) => UpdateOverflow();

    private void UpdateOverflow()
    {
        var items = Items;
        var count = items?.Count ?? 0;
        if (count == 0 || MetricsViewport.ActualWidth <= 0)
        {
            MoreButton.Visibility = Visibility.Hidden;
            return;
        }

        _visibleCount = Math.Min(count, Math.Max(1, (int)Math.Floor(MetricsViewport.ActualWidth / MetricCardStride)));
        var overflowCount = Math.Max(0, count - _visibleCount);
        MoreButton.Content = $"+{overflowCount}";
        MoreButton.Visibility = overflowCount > 0 ? Visibility.Visible : Visibility.Hidden;
        PopupCountText.Text = $"{overflowCount} 项";

        if (!_isDragging)
        {
            _overflowItems.Clear();
            foreach (var metric in items!.Skip(_visibleCount))
            {
                _overflowItems.Add(metric);
            }
        }
    }

    private void More_Click(object sender, RoutedEventArgs e)
    {
        UpdateOverflow();
        OverflowPopup.IsOpen = true;
    }

    private void Clear_Click(object sender, RoutedEventArgs e) => ClearRequested?.Invoke();
}
