using LogAnalyzer.Models;
using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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

        var entries = Entries.OfType<LogFileEntry>().ToList();
        if (entries.Count == 0)
        {
            return;
        }

        var topOffset = GetTopOffset();
        var bottomOffset = GetBottomOffset();
        var drawableHeight = Math.Max(0, height - topOffset - bottomOffset);
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

            var y = topOffset + (i * slotHeight);
            var marker = Math.Min(markerHeight, (topOffset + drawableHeight) - y);
            if (marker <= 0)
            {
                continue;
            }

            drawingContext.DrawRectangle(brush, null, new Rect(0, y, width, marker));
        }
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
