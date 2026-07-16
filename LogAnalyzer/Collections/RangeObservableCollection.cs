using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace LogAnalyzer.Collections;

/// <summary>
/// An <see cref="ObservableCollection{T}"/> that can add many items while raising
/// only a single change notification, avoiding per-item UI churn during bulk loads.
/// </summary>
public sealed class RangeObservableCollection<T> : ObservableCollection<T>
{
    public RangeObservableCollection()
    {
    }

    public RangeObservableCollection(IEnumerable<T> collection) : base(collection)
    {
    }

    /// <summary>
    /// Adds the supplied items and raises a single Reset notification instead of
    /// one Add notification per item.
    /// </summary>
    public void AddRange(IEnumerable<T> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        var added = false;
        foreach (var item in items)
        {
            Items.Add(item);
            added = true;
        }

        if (!added)
        {
            return;
        }

        OnPropertyChanged(new System.ComponentModel.PropertyChangedEventArgs(nameof(Count)));
        OnPropertyChanged(new System.ComponentModel.PropertyChangedEventArgs("Item[]"));
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }
}
