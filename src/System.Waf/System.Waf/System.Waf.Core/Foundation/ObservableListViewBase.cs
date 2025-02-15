﻿using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Threading;

namespace System.Waf.Foundation
{
    /// <summary>Provides the base class for a generic observable read-only collection.</summary>
    /// <typeparam name="T">The type of elements in the collection.</typeparam>
    public abstract class ObservableListViewBase<T> : ReadOnlyCollection<T>, INotifyPropertyChanged, IReadOnlyObservableList<T>
    {
        private const string indexerName = "Item[]";  // This must be equal to Binding.IndexerName
        private static readonly PropertyChangedEventArgs CountChangedEventArgs = new PropertyChangedEventArgs(nameof(Count));
        private static readonly PropertyChangedEventArgs IndexerChangedEventArgs = new PropertyChangedEventArgs(indexerName);
        private static readonly NotifyCollectionChangedEventArgs CollectionResetEventArgs = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset);
        
        private volatile int deferCount;
        private int deferredChanges;

        /// <summary>Initializes a new instance of the ObservableListViewBase class.</summary>
        /// <param name="originalList">Initialize the list view with the items from this list.</param>
        protected ObservableListViewBase(IEnumerable<T>? originalList) : base(new List<T>())
        {
            InnerList = (List<T>)Items;
            if (originalList != null) InnerList.AddRange(originalList);
        }

        /// <summary>The inner list of this list view.</summary>
        protected List<T> InnerList { get; }

        /// <summary>Occurs when an item is added, removed, changed, moved, or the entire list is refreshed.</summary>
        public event NotifyCollectionChangedEventHandler? CollectionChanged;

        /// <summary>Occurs when a property value changes.</summary>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>Defer collection changed notifications until Dispose is called on the returned object. If the collection was changed then a CollectionChanged Reset event will be raised.</summary>
        /// <returns>Object used to stop the deferral.</returns>
        public IDisposable DeferCollectionChangedNotifications()
        {
            Interlocked.Increment(ref deferCount);
            return new DisposedNotifier(() =>
            {
                if (Interlocked.Decrement(ref deferCount) == 0 && Interlocked.Exchange(ref deferredChanges, 0) > 0)
                {
                    OnCollectionChanged(CollectionResetEventArgs);
                }
            });
        }

        /// <summary>Raises the CollectionChanged event with the provided arguments.</summary>
        /// <param name="e">Arguments of the event being raised.</param>
        protected virtual void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            if (deferCount > 0) { Interlocked.Increment(ref deferredChanges); }
            else { CollectionChanged?.Invoke(this, e); }
        }

        /// <summary>Raises the PropertyChanged event with the provided arguments.</summary>
        /// <param name="e">Arguments of the event being raised.</param>
        protected virtual void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            PropertyChanged?.Invoke(this, e);
        }

        /// <summary>Inserts an element into the list at the specified index.</summary>
        /// <param name="newItemIndex">The zero-based index at which item should be inserted.</param>
        /// <param name="newItem">The object to insert.</param>
        protected void Insert(int newItemIndex, [AllowNull] T newItem)
        {
            InnerList.Insert(newItemIndex, newItem!);
            OnPropertyChanged(CountChangedEventArgs);
            OnPropertyChanged(IndexerChangedEventArgs);
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, newItem, newItemIndex));
        }

        /// <summary>Removes the element at the specified index.</summary>
        /// <param name="oldItemIndex">The zero-based index of the element to remove.</param>
        protected void RemoveAt(int oldItemIndex)
        {
            var oldItem = InnerList[oldItemIndex];
            InnerList.RemoveAt(oldItemIndex);
            OnPropertyChanged(CountChangedEventArgs);
            OnPropertyChanged(IndexerChangedEventArgs);
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, oldItem, oldItemIndex));
        }

        /// <summary>Replace all items with the items of the new list.</summary>
        /// <param name="newList">The items of the new list.</param>
        protected void Reset(IEnumerable<T> newList)
        {
            InnerList.Clear();
            InnerList.AddRange(newList);
            OnPropertyChanged(CountChangedEventArgs);
            OnPropertyChanged(IndexerChangedEventArgs);
            OnCollectionChanged(CollectionResetEventArgs);
        }

        /// <summary>Moves the item at the specified index to a new location in the collection.</summary>
        /// <param name="oldIndex">The zero-based index specifying the location of the item to be moved.</param>
        /// <param name="newIndex">The zero-based index specifying the new location of the item.</param>
        protected void Move(int oldIndex, int newIndex)
        {
            T item = InnerList[oldIndex];
            InnerList.RemoveAt(oldIndex);
            InnerList.Insert(newIndex, item);
            OnPropertyChanged(IndexerChangedEventArgs);
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Move, item, newIndex, oldIndex));
        }

        private sealed class DisposedNotifier : IDisposable
        {
            private Action? disposed;

            public DisposedNotifier(Action disposed)
            {
                this.disposed = disposed;
            }

            public void Dispose() => Interlocked.Exchange(ref disposed, null)?.Invoke();
        }
    }
}
