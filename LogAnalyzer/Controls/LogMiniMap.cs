using LogAnalyzer.Models;
using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace LogAnalyzer.Controls;

public sealed class LogMiniMap : FrameworkElement
{
    public static readonly DependencyProperty EntriesProperty =
        DependencyProperty.Register(
            nameof(Entries),
            typeof(IEnumerable),
            typeof(LogMiniMap),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnEntriesChanged));

    public static readonly DependencyProperty MinimumMarkerHeightProperty =
        DependencyProperty.Register(
            nameof(MinimumMarkerHeight),
            typeof(double),
            typeof(LogMiniMap),
            new FrameworkPropertyMetadata(1.0d, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty SourceListViewProperty =
        DependencyProperty.Register(
            nameof(SourceListView),
            typeof(ListView),
            typeof(LogMiniMap),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnSourceListViewChanged));

    private INotifyCollectionChanged? _currentCollection;
    private readonly List<INotifyPropertyChanged> _subscribedEntries = [];
    private ListView? _attachedListView;
    private readonly Dictionary<string, SolidColorBrush?> _brushCache = new(StringComparer.OrdinalIgnoreCase);
    private bool _renderRequested;
    private bool _isScrubbing;
    private readonly ToolTip _hoverToolTip = new()
    {
        Placement = PlacementMode.Mouse,
        StaysOpen = true
    };

    public IEnumerable? Entries
    {
        get => (IEnumerable?)GetValue(EntriesProperty);
        set => SetValue(EntriesProperty, value);
    }

    public double MinimumMarkerHeight
    {
        get => (double)GetValue(MinimumMarkerHeightProperty);
        set => SetValue(MinimumMarkerHeightProperty, value);
    }

    public ListView? SourceListView
    {
        get => (ListView?)GetValue(SourceListViewProperty);
        set => SetValue(SourceListViewProperty, value);
    }

    public LogMiniMap()
    {
        SizeChanged += (_, _) => RequestRender();
        Unloaded += (_, _) => DetachAll();
    }

    /// <summary>
    /// Coalesces repeated invalidations (e.g. during bulk parsing/highlighting)
    /// into a single deferred render pass.
    /// </summary>
    private void RequestRender()
    {
        if (_renderRequested)
        {
            return;
        }

        _renderRequested = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
        {
            _renderRequested = false;
            InvalidateVisual();
        }));
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        var width = ActualWidth;
        var height = ActualHeight;
        if (width <= 0 || height <= 0 || Entries is null || !IsVisible)
        {
            return;
        }

        // Ensure the whole minimap area participates in hit testing,
        // even where no colored marker is drawn.
        drawingContext.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, width, height));

        var entries = Entries.OfType<LogFileEntry>().ToList();
        if (entries.Count == 0)
        {
            return;
        }

        var topOffset = 0d;
        var drawableHeight = height;
        if (drawableHeight <= 0)
        {
            return;
        }

        var slotHeight = drawableHeight / entries.Count;
        var markerHeight = Math.Max(MinimumMarkerHeight, slotHeight);

        for (var i = 0; i < entries.Count; i++)
        {
            var colorHex = entries[i].HighlightColor;
            if (string.IsNullOrWhiteSpace(colorHex))
            {
                continue;
            }

            var brush = GetBrush(colorHex);
            if (brush is null)
            {
                continue;
            }

            var y = i * slotHeight;
            var marker = Math.Min(markerHeight, drawableHeight - y);
            if (marker <= 0)
            {
                continue;
            }

            drawingContext.DrawRectangle(brush, null, new Rect(0, y, width, marker));
        }
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);

        _isScrubbing = true;
        CaptureMouse();
        _hoverToolTip.IsOpen = false;

        NavigateToPosition(e.GetPosition(this), selectEntry: true);
        e.Handled = true;
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);

        if (!_isScrubbing)
        {
            return;
        }

        _isScrubbing = false;
        if (IsMouseCaptured)
        {
            ReleaseMouseCapture();
        }

        NavigateToPosition(e.GetPosition(this), selectEntry: true);
        e.Handled = true;
    }

    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        base.OnLostMouseCapture(e);
        _isScrubbing = false;
    }

    private void NavigateToPosition(Point position, bool selectEntry)
    {
        var listView = SourceListView ?? _attachedListView;
        if (listView is null)
        {
            return;
        }

        var entries = Entries?.OfType<LogFileEntry>().ToList();
        if (entries is null || entries.Count == 0 || ActualHeight <= 0)
        {
            return;
        }

        var ratio = Math.Clamp(position.Y / ActualHeight, 0d, 1d);
        var targetIndex = (int)Math.Floor(ratio * entries.Count);
        targetIndex = Math.Clamp(targetIndex, 0, entries.Count - 1);
        var targetEntry = entries[targetIndex];

        var scrollViewer = FindVisualChild<ScrollViewer>(listView);
        if (scrollViewer is not null && scrollViewer.ScrollableHeight > 0)
        {
            var offset = ratio * scrollViewer.ScrollableHeight;
            scrollViewer.ScrollToVerticalOffset(offset);
        }
        else
        {
            listView.ScrollIntoView(targetEntry);
        }

        if (selectEntry)
        {
            listView.SelectedItem = targetEntry;
            listView.ScrollIntoView(targetEntry);
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (_isScrubbing && IsMouseCaptured)
        {
            _hoverToolTip.IsOpen = false;
            NavigateToPosition(e.GetPosition(this), selectEntry: false);
            e.Handled = true;
            return;
        }

        if (!TryGetEntryFromPosition(e.GetPosition(this), out var entry, requireHighlighted: true) || entry is null)
        {
            _hoverToolTip.IsOpen = false;
            return;
        }

        var textPreview = entry.Text;
        if (textPreview.Length > 120)
        {
            textPreview = textPreview[..120] + "…";
        }

        _hoverToolTip.Content = $"Line {entry.LineNumber}\n{entry.DateDisplay}\n{textPreview}";
        _hoverToolTip.PlacementTarget = this;
        _hoverToolTip.IsOpen = true;
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        _hoverToolTip.IsOpen = false;
    }

    private bool TryGetEntryFromPosition(Point position, out LogFileEntry? entry, bool requireHighlighted)
    {
        entry = null;

        var entries = Entries?.OfType<LogFileEntry>().ToList();
        if (entries is null || entries.Count == 0)
        {
            return false;
        }

        var topOffset = 0d;
        var drawableHeight = ActualHeight;
        if (drawableHeight <= 0)
        {
            return false;
        }

        var slotHeight = drawableHeight / entries.Count;
        if (slotHeight <= 0)
        {
            return false;
        }

        var relativeY = Math.Clamp(position.Y - topOffset, 0, Math.Max(0, drawableHeight - 0.0001d));

        if (!requireHighlighted)
        {
            var index = (int)(relativeY / slotHeight);
            index = Math.Clamp(index, 0, entries.Count - 1);
            entry = entries[index];
            return true;
        }

        var markerHeight = Math.Max(MinimumMarkerHeight, slotHeight);
        var estimatedIndex = (int)(relativeY / slotHeight);
        estimatedIndex = Math.Clamp(estimatedIndex, 0, entries.Count - 1);

        var searchStart = Math.Max(0, estimatedIndex - 2);
        var searchEnd = Math.Min(entries.Count - 1, estimatedIndex + 2);

        for (var i = searchStart; i <= searchEnd; i++)
        {
            var candidate = entries[i];
            if (string.IsNullOrWhiteSpace(candidate.HighlightColor) || GetBrush(candidate.HighlightColor) is null)
            {
                continue;
            }

            var markerY = i * slotHeight;
            var markerBottom = Math.Min(markerY + markerHeight, drawableHeight);
            if (relativeY >= markerY && relativeY <= markerBottom)
            {
                entry = candidate;
                return true;
            }
        }

        return false;
    }

    private SolidColorBrush? GetBrush(string colorHex)
    {
        if (_brushCache.TryGetValue(colorHex, out var cached))
        {
            return cached;
        }

        SolidColorBrush? brush = null;
        try
        {
            if (ColorConverter.ConvertFromString(colorHex) is Color color)
            {
                brush = new SolidColorBrush(color);
                brush.Freeze();
            }
        }
        catch
        {
        }

        _brushCache[colorHex] = brush;
        return brush;
    }

    private static void OnEntriesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not LogMiniMap miniMap)
        {
            return;
        }

        miniMap.AttachToEntries(e.NewValue as IEnumerable);
        miniMap.RequestRender();
    }

    private static void OnSourceListViewChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not LogMiniMap miniMap)
        {
            return;
        }

        miniMap.AttachListView(e.OldValue as ListView, e.NewValue as ListView);
        miniMap.RequestRender();
    }

    private void AttachListView(ListView? oldListView, ListView? newListView)
    {
        if (oldListView is not null)
        {
            oldListView.Loaded -= SourceListViewOnLayoutChanged;
            oldListView.SizeChanged -= SourceListViewOnLayoutChanged;
            oldListView.LayoutUpdated -= SourceListViewOnLayoutUpdated;
        }

        _attachedListView = newListView;

        if (newListView is not null)
        {
            newListView.Loaded += SourceListViewOnLayoutChanged;
            newListView.SizeChanged += SourceListViewOnLayoutChanged;
            newListView.LayoutUpdated += SourceListViewOnLayoutUpdated;
        }
    }

    private void SourceListViewOnLayoutChanged(object? sender, RoutedEventArgs e)
    {
        RequestRender();
    }

    private void SourceListViewOnLayoutChanged(object? sender, SizeChangedEventArgs e)
    {
        RequestRender();
    }

    private void SourceListViewOnLayoutUpdated(object? sender, EventArgs e)
    {
        RequestRender();
    }

    private double GetTopOffset()
    {
        var listView = SourceListView ?? _attachedListView;
        if (listView is null)
        {
            return 0;
        }

        var presenter = FindVisualChild<GridViewHeaderRowPresenter>(listView);
        return (presenter?.ActualHeight ?? 0) + listView.BorderThickness.Top + listView.Padding.Top;
    }

    private double GetBottomOffset()
    {
        var listView = SourceListView ?? _attachedListView;
        if (listView is null)
        {
            return 0;
        }

        var horizontalScrollBarHeight = 0d;
        var horizontalScrollBar = FindVisibleHorizontalScrollBar(listView);
        if (horizontalScrollBar is not null)
        {
            horizontalScrollBarHeight = horizontalScrollBar.ActualHeight;
        }

        return horizontalScrollBarHeight + listView.BorderThickness.Bottom + listView.Padding.Bottom;
    }

    private static ScrollBar? FindVisibleHorizontalScrollBar(DependencyObject parent)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is ScrollBar scrollBar
                && scrollBar.Orientation == Orientation.Horizontal
                && scrollBar.Visibility == Visibility.Visible
                && scrollBar.ActualHeight > 0)
            {
                return scrollBar;
            }

            var result = FindVisibleHorizontalScrollBar(child);
            if (result is not null)
            {
                return result;
            }
        }

        return null;
    }

    private void AttachToEntries(IEnumerable? entries)
    {
        DetachAll();

        if (entries is INotifyCollectionChanged collectionChanged)
        {
            _currentCollection = collectionChanged;
            _currentCollection.CollectionChanged += OnCollectionChanged;
        }

        if (entries is null)
        {
            return;
        }

        foreach (var notifyPropertyChanged in entries.OfType<INotifyPropertyChanged>())
        {
            notifyPropertyChanged.PropertyChanged += OnEntryPropertyChanged;
            _subscribedEntries.Add(notifyPropertyChanged);
        }
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            if (Entries is IEnumerable current)
            {
                AttachToEntries(current);
            }

            RequestRender();
            return;
        }

        if (e.OldItems is not null)
        {
            foreach (var oldItem in e.OldItems.OfType<INotifyPropertyChanged>())
            {
                oldItem.PropertyChanged -= OnEntryPropertyChanged;
                _subscribedEntries.Remove(oldItem);
            }
        }

        if (e.NewItems is not null)
        {
            foreach (var newItem in e.NewItems.OfType<INotifyPropertyChanged>())
            {
                if (_subscribedEntries.Contains(newItem))
                {
                    continue;
                }

                newItem.PropertyChanged += OnEntryPropertyChanged;
                _subscribedEntries.Add(newItem);
            }
        }

        RequestRender();
    }

    private void OnEntryPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == nameof(LogFileEntry.HighlightColor))
        {
            RequestRender();
        }
    }

    private void DetachAll()
    {
        _hoverToolTip.IsOpen = false;

        if (_attachedListView is not null)
        {
            _attachedListView.Loaded -= SourceListViewOnLayoutChanged;
            _attachedListView.SizeChanged -= SourceListViewOnLayoutChanged;
            _attachedListView.LayoutUpdated -= SourceListViewOnLayoutUpdated;
            _attachedListView = null;
        }

        if (_currentCollection is not null)
        {
            _currentCollection.CollectionChanged -= OnCollectionChanged;
            _currentCollection = null;
        }

        foreach (var notifyPropertyChanged in _subscribedEntries)
        {
            notifyPropertyChanged.PropertyChanged -= OnEntryPropertyChanged;
        }

        _subscribedEntries.Clear();
    }

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T typed)
            {
                return typed;
            }

            var result = FindVisualChild<T>(child);
            if (result is not null)
            {
                return result;
            }
        }

        return null;
    }
}
