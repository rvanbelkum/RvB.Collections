using System.Collections;
using System.Runtime.CompilerServices;

namespace RvB.Collections;

/// <summary>
/// Represents a collection of elements that are automatically maintained in sorted order.
/// </summary>
/// <remarks>The <see cref="OrderedList{T}"/> ensures that elements are always stored in sorted order according to
/// the specified comparer or the default comparer for the type <typeparamref name="T"/>. Items are inserted into their
/// correct position, and direct manipulation of the order (e.g., via <see cref="Insert(int, T)"/> or setting an indexed
/// item) is not supported.</remarks>
/// <typeparam name="T">The type of elements in the list. The type must be comparable, either by implementing <see cref="IComparable{T}"/>
/// or by providing a custom <see cref="IComparer{T}"/>.</typeparam>
[CollectionBuilder(typeof(OrderedListBuilder), nameof(OrderedListBuilder.Create))]
public sealed class OrderedList<T> : IList<T>, IReadOnlyList<T> {
    private readonly IComparer<T> _comparer;
    private readonly List<T> _innerList;

    /// <summary>
    /// Initializes a new instance of the <see cref="OrderedList{T}"/> class with the default comparer and an initial
    /// capacity of zero.
    /// </summary>
    /// <remarks>This constructor creates an empty ordered list using the default comparer for the type
    /// <typeparamref name="T"/>. The list will automatically grow as items are added.</remarks>
    public OrderedList() : this(0, Comparer<T>.Default) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="OrderedList{T}"/> class with the specified default size and the
    /// default comparer.
    /// </summary>
    /// <remarks>This constructor creates an ordered list with the specified initial capacity and uses the
    /// default comparer for the type <typeparamref name="T"/>. If <paramref name="defaultSize"/> is 0, the list will be
    /// initialized with no pre-allocated storage.</remarks>
    /// <param name="defaultSize">The initial capacity of the list. Must be greater than or equal to 0.</param>
    public OrderedList(int defaultSize) : this(defaultSize, Comparer<T>.Default) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="OrderedList{T}"/> class with the specified comparer.
    /// </summary>
    /// <param name="comparer">The comparer used to determine the order of elements in the list. If <paramref name="comparer"/> is <see
    /// langword="null"/>, the default comparer for the type <typeparamref name="T"/> is used.</param>
    public OrderedList(IComparer<T> comparer) : this(0, comparer) { }


    public OrderedList(IEnumerable<T> items) : this(items, Comparer<T>.Default) { }

    public OrderedList(IEnumerable<T> items, IComparer<T> comparer) {
        _comparer = comparer ?? throw new ArgumentNullException(nameof(comparer));
        _innerList = [.. items.Order(comparer)];
    }


    /// <summary>
    /// Initializes a new instance of the <see cref="OrderedList{T}"/> class with the specified default size and
    /// comparer.
    /// </summary>
    /// <param name="defaultSize">The initial capacity of the list. Must be a non-negative value.</param>
    /// <param name="comparer">The comparer used to determine the order of elements in the list. Cannot be <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="comparer"/> is <see langword="null"/>.</exception>
    public OrderedList(int defaultSize, IComparer<T> comparer) {
        ArgumentOutOfRangeException.ThrowIfNegative(defaultSize);
        _comparer = comparer ?? throw new ArgumentNullException(nameof(comparer));
        _innerList = new(defaultSize);
    }

    /// <summary>
    /// Gets the element at the specified index in the sorted list.
    /// </summary>
    /// <param name="index">The zero-based index of the element to retrieve.</param>
    /// <returns>The element at the specified index in the sorted list.</returns>
    public T this[int index] {
        get => _innerList[index];
        set => throw new NotSupportedException("Cannot set an indexed item in a sorted list.");
    }

    /// <summary>
    /// Gets the number of elements contained in the collection.
    /// </summary>
    public int Count
        => _innerList.Count;

    public bool IsReadOnly
        => false;

    /// <summary>
    /// Adds an item to the collection while maintaining the collection's sorted order.
    /// </summary>
    /// <remarks>The item is inserted at the appropriate position to ensure the collection remains sorted.  If
    /// the item already exists in the collection, it will be inserted before or after existing items,  depending on the
    /// comparer.</remarks>
    /// <param name="item">The item to add to the collection. Must be compatible with the comparer used by the collection.</param>
    public void Add(T item) {
        int index = _innerList.BinarySearch(item, _comparer);
        index = (index >= 0) ? index : ~index;
        _innerList.Insert(index, item);
    }

    public bool TryAdd(T item, out int index) {
        index = _innerList.BinarySearch(item, _comparer);
        if (index >= 0) {
            return false;
        }
        index = ~index;
        _innerList.Insert(index, item);
        return true;
    }

    public bool TryGet(T item, out int index) {
        index = _innerList.BinarySearch(item, _comparer);
        if (index >= 0) {
            return true;
        }
        index = ~index;
        return false;
    }

    /// <summary>
    /// Removes all elements from the collection.
    /// </summary>
    /// <remarks>After calling this method, the collection will be empty. This operation does not modify the
    /// capacity of the underlying storage.</remarks>
    public void Clear()
        => _innerList.Clear();

    /// <summary>
    /// Determines whether the collection contains the specified item.
    /// </summary>
    /// <remarks>The comparison is performed using the specified comparer.</remarks>
    /// <param name="item">The item to locate in the collection.</param>
    /// <returns><see langword="true"/> if the item is found in the collection; otherwise, <see langword="false"/>.</returns>
    public bool Contains(T item) {
        return _innerList.BinarySearch(item, _comparer) >= 0;
    }

    public void CopyTo(T[] array, int arrayIndex)
        => _innerList.CopyTo(array, arrayIndex);

    /// <summary>
    /// Searches for the specified item in the collection and returns the zero-based index of the first occurrence.
    /// </summary>
    /// <remarks>The comparison is performed using the specified comparer.</remarks>
    /// <param name="item">The item to locate in the collection.</param>
    /// <returns>The zero-based index of the item if found; otherwise, -1 if the item is not present in the collection.</returns>
    public int IndexOf(T item) {
        var index = _innerList.BinarySearch(item, _comparer);
        if (index >= 0)
            return index;
        return -1;
    }

    /// <summary>
    /// Throws a <see cref="NotSupportedException"/> to indicate that inserting an item at a specific index is not
    /// supported in a sorted list.
    /// </summary>
    /// <param name="index">The zero-based index at which the item would be inserted.</param>
    /// <param name="item">The item to insert into the list.</param>
    /// <exception cref="NotSupportedException">Always thrown to indicate that this operation is not supported.</exception>
    public void Insert(int index, T item)
        => throw new NotSupportedException("Cannot insert an indexed item in a sorted list.");

    /// <summary>
    /// Removes the specified item from the collection.
    /// </summary>
    /// <remarks>The comparison is performed using the specified comparer.</remarks>
    /// <param name="item">The item to remove from the collection.</param>
    /// <returns><see langword="true"/> if the item was successfully removed;  otherwise, <see langword="false"/> if the item was
    /// not found in the collection. </returns>
    public bool Remove(T item) {
        var index = _innerList.BinarySearch(item, _comparer);
        if (index >= 0) {
            _innerList.RemoveAt(index);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Removes and returns the first (minimal) item from the collection.
    /// </summary>
    /// <returns>The removed element</returns>
    public T PopMin() {
        var popped = _innerList[0];
        _innerList.RemoveAt(0);
        return popped;
    }


    /// <summary>
    /// Removes the element at the specified index from the collection.
    /// </summary>
    /// <param name="index">The zero-based index of the element to remove. Must be within the bounds of the collection.</param>
    public void RemoveAt(int index)
        => _innerList.RemoveAt(index);

    public IEnumerator<T> GetEnumerator()
        => _innerList.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();
}

public class OrderedListBuilder {
    public static OrderedList<T> Create<T>(ReadOnlySpan<T> items) {
        var list = new OrderedList<T>(items.Length);
        foreach (var item in items) {
            list.Add(item);
        }
        return list;
    }
}

// <summary>
/// Represents a collection of elements that are automatically maintained in sorted order.
/// </summary>
/// <remarks>The <see cref="OrderedList{T}"/> ensures that elements are always stored in sorted order according to
/// the specified comparer or the default comparer for the type <typeparamref name="T"/>. Items are inserted into their
/// correct position, and direct manipulation of the order (e.g., via <see cref="Insert(int, T)"/> or setting an indexed
/// item) is not supported.</remarks>
/// <typeparam name="T">The type of elements in the list. The type must be comparable, either by implementing <see cref="IComparable{T}"/>
/// or by providing a custom <see cref="IComparer{T}"/>.</typeparam>
[CollectionBuilder(typeof(OrderedListBuilder2), nameof(OrderedListBuilder2.Create))]
public sealed class OrderedList<TKey, TValue> : IReadOnlyList<(TKey Key, TValue Value)> {
    private readonly KeyValueComparer _comparer;
    private readonly List<(TKey, TValue)> _innerList;

    /// <summary>
    /// Initializes a new instance of the <see cref="OrderedList{T}"/> class with the default comparer and an initial
    /// capacity of zero.
    /// </summary>
    /// <remarks>This constructor creates an empty ordered list using the default comparer for the type
    /// <typeparamref name="TKey"/>. The list will automatically grow as items are added.</remarks>
    public OrderedList() : this(0, Comparer<TKey>.Default) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="OrderedList{T}"/> class with the specified default size and the
    /// default comparer.
    /// </summary>
    /// <remarks>This constructor creates an ordered list with the specified initial capacity and uses the
    /// default comparer for the type <typeparamref name="TKey"/>. If <paramref name="defaultSize"/> is 0, the list will be
    /// initialized with no pre-allocated storage.</remarks>
    /// <param name="defaultSize">The initial capacity of the list. Must be greater than or equal to 0.</param>
    public OrderedList(int defaultSize) : this(defaultSize, Comparer<TKey>.Default) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="OrderedList{T}"/> class with the specified comparer.
    /// </summary>
    /// <param name="comparer">The comparer used to determine the order of elements in the list. If <paramref name="comparer"/> is <see
    /// langword="null"/>, the default comparer for the type <typeparamref name="TKey"/> is used.</param>
    public OrderedList(IComparer<TKey> comparer) : this(0, comparer) { }


    public OrderedList(IEnumerable<(TKey, TValue)> items) : this(items, Comparer<TKey>.Default) { }

    public OrderedList(IEnumerable<(TKey, TValue)> items, IComparer<TKey> comparer) {
        ArgumentNullException.ThrowIfNull(comparer);
        _comparer = new(comparer);
        _innerList = [.. items.Order(_comparer)];
    }


    /// <summary>
    /// Initializes a new instance of the <see cref="OrderedList{T}"/> class with the specified default size and
    /// comparer.
    /// </summary>
    /// <param name="defaultSize">The initial capacity of the list. Must be a non-negative value.</param>
    /// <param name="comparer">The comparer used to determine the order of elements in the list. Cannot be <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="comparer"/> is <see langword="null"/>.</exception>
    public OrderedList(int defaultSize, IComparer<TKey> comparer) {
        ArgumentNullException.ThrowIfNull(comparer);
        ArgumentOutOfRangeException.ThrowIfNegative(defaultSize);
        _comparer = new(comparer);
        _innerList = new(defaultSize);
    }

    /// <summary>
    /// Gets the element at the specified index in the sorted list.
    /// </summary>
    /// <param name="index">The zero-based index of the element to retrieve.</param>
    /// <returns>The element at the specified index in the sorted list.</returns>
    public (TKey Key, TValue Value) this[int index] {
        get => _innerList[index];
        set => throw new NotSupportedException("Cannot set an indexed item in a sorted list.");
    }

    /// <summary>
    /// Gets the number of elements contained in the collection.
    /// </summary>
    public int Count
        => _innerList.Count;

    public bool IsReadOnly
        => false;

    /// <summary>
    /// Adds an item to the collection while maintaining the collection's sorted order.
    /// </summary>
    /// <remarks>The item is inserted at the appropriate position to ensure the collection remains sorted.</remarks>
    /// <param name="item">The item to add to the collection. Must be compatible with the comparer used by the collection.</param>
    public void Add((TKey, TValue) item) {
        int index = _innerList.BinarySearch(item, _comparer);
        index = (index >= 0) ? index : ~index;
        _innerList.Insert(index, item);
    }

    /// <summary>
    /// Attempts to add the specified key-value pair to the collection. Returns a value indicating whether the addition
    /// was successful.
    /// </summary>
    /// <remarks>If the key already exists in the collection, the method does not add the item and returns
    /// false. The index output parameter provides the location of the item, whether newly added or already present. The
    /// collection maintains items in sorted order according to the comparer.</remarks>
    /// <param name="item">The key-value pair to add to the collection, represented as a tuple containing the key and value.</param>
    /// <param name="index">When this method returns, contains the index at which the item was added if successful, or the index of the
    /// existing item if the key already exists. This parameter is passed uninitialized.</param>
    /// <returns>true if the item was successfully added to the collection; otherwise, false if the key already exists.</returns>
    public bool TryAdd((TKey, TValue) item, out int index) {
        index = _innerList.BinarySearch(item, _comparer);
        if (index >= 0) {
            return false;
        }
        index = ~index;
        _innerList.Insert(index, item);
        return true;
    }

    public bool TryGet(TKey item, out int index) {
        index = _innerList.BinarySearch((item, default!), _comparer);
        if (index >= 0) {
            return true;
        }
        index = ~index;
        return false;
    }

    /// <summary>
    /// Removes all elements from the collection.
    /// </summary>
    /// <remarks>After calling this method, the collection will be empty. This operation does not modify the
    /// capacity of the underlying storage.</remarks>
    public void Clear()
        => _innerList.Clear();

    /// <summary>
    /// Determines whether the collection contains the specified item.
    /// </summary>
    /// <remarks>The comparison is performed using the specified comparer.</remarks>
    /// <param name="item">The item to locate in the collection.</param>
    /// <returns><see langword="true"/> if the item is found in the collection; otherwise, <see langword="false"/>.</returns>
    public bool Contains(TKey item) {
        return _innerList.BinarySearch((item, default!), _comparer) >= 0;
    }

    public void CopyTo((TKey, TValue)[] array, int arrayIndex)
        => _innerList.CopyTo(array, arrayIndex);

    /// <summary>
    /// Searches for the specified item in the collection and returns the zero-based index of the first occurrence.
    /// </summary>
    /// <remarks>The comparison is performed using the specified comparer.</remarks>
    /// <param name="item">The item to locate in the collection.</param>
    /// <returns>The zero-based index of the item if found; otherwise, -1 if the item is not present in the collection.</returns>
    public int IndexOf(TKey item) {
        var index = _innerList.BinarySearch((item, default!), _comparer);
        if (index >= 0)
            return index;
        return -1;
    }

    /// <summary>
    /// Throws a <see cref="NotSupportedException"/> to indicate that inserting an item at a specific index is not supported in a sorted list.
    /// </summary>
    /// <param name="index">The zero-based index at which the item would be inserted.</param>
    /// <param name="item">The item to insert into the list.</param>
    /// <exception cref="NotSupportedException">Always thrown to indicate that this operation is not supported.</exception>
    public void Insert(int index, TKey item)
        => throw new NotSupportedException("Cannot insert an indexed item in a sorted list.");

    /// <summary>
    /// Removes the specified item from the collection.
    /// </summary>
    /// <remarks>The comparison is performed using the specified comparer.</remarks>
    /// <param name="item">The item to remove from the collection.</param>
    /// <returns><see langword="true"/> if the item was successfully removed;  otherwise, <see langword="false"/> if the item was
    /// not found in the collection. </returns>
    public bool Remove(TKey item) {
        var index = _innerList.BinarySearch((item, default!), _comparer);
        if (index >= 0) {
            _innerList.RemoveAt(index);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Removes and returns the first (minimal) item from the collection.
    /// </summary>
    /// <returns>The removed element</returns>
    public (TKey, TValue) PopMin() {
        var popped = _innerList[0];
        _innerList.RemoveAt(0);
        return popped;
    }


    /// <summary>
    /// Removes the element at the specified index from the collection.
    /// </summary>
    /// <param name="index">The zero-based index of the element to remove. Must be within the bounds of the collection.</param>
    public void RemoveAt(int index)
        => _innerList.RemoveAt(index);

    public IEnumerator<(TKey, TValue)> GetEnumerator()
        => _innerList.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();

    private sealed class KeyValueComparer : IComparer<(TKey, TValue)> {
        private readonly IComparer<TKey> _keyComparer;
        public KeyValueComparer(IComparer<TKey> keyComparer) {
            _keyComparer = keyComparer;
        }
        public int Compare((TKey, TValue) x, (TKey, TValue) y) {
            return _keyComparer.Compare(x.Item1, y.Item1);
        }
    }
}

public class OrderedListBuilder2 {
    public static OrderedList<TKey, TValue> Create<TKey, TValue>(ReadOnlySpan<(TKey, TValue)> items) {
        var list = new OrderedList<TKey, TValue>(items.Length);
        foreach (var item in items) {
            list.Add(item);
        }
        return list;
    }
}
