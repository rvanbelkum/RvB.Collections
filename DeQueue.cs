using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace RvB.Collections;

/// <summary>
/// Represents a double-ended queue that allows elements to be added or removed from both ends.
/// </summary>
/// <remarks>The <see cref="Dequeue{T}"/> class provides efficient operations for adding and removing elements
/// from both the front and the back of the queue. It supports dynamic resizing and ensures that the capacity is
/// adjusted as needed, up to the specified maximum size.</remarks>
/// <typeparam name="T">The type of elements stored in the queue.</typeparam>
[CollectionBuilder(typeof(DequeueBuilder), nameof(DequeueBuilder.Create))]
public sealed class Dequeue<T> : IReadOnlyCollection<T> {
    private T[] _buffer;
    private int _firstIndex;
    private int _count;
    private int _version;
    private readonly int _maxSize;

    /// <summary>
    /// Initializes a new instance of the <see cref="Dequeue{T}"/> class.
    /// </summary>
    public Dequeue() : this(0) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="Dequeue{T}"/> class with the specified capacity.
    /// </summary>
    /// <param name="size">The initial capacity of the queue. Must be greater than zero.</param>
    public Dequeue(int size) : this(size, int.MaxValue) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="Dequeue{T}"/> class with the specified initial size and maximum size.
    /// </summary>
    /// <param name="size">The initial size of the queue. Must be at least zero.</param>
    /// <param name="maxSize">The maximum size of the queue. Must be greater than or equal to <paramref name="size"/>.</param>
    public Dequeue(int size, int maxSize) {
        ArgumentOutOfRangeException.ThrowIfNegative(size, nameof(size));
        ArgumentOutOfRangeException.ThrowIfLessThan(maxSize, size, nameof(maxSize));
        _buffer = new T[size];
        _firstIndex = 0;
        _count = 0;
        _maxSize = maxSize;
        _version = 0;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Dequeue{T}"/> class and populates it with the specified items.
    /// </summary>
    /// <remarks>The queue is initialized with the elements from the provided collection in the order they are enumerated.</remarks>
    /// <param name="items">The collection of items to initialize the queue with. Cannot be null.</param>
    public Dequeue(IEnumerable<T> items) : this(items, int.MaxValue) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="Dequeue{T}"/> class with the specified items and maximum size.
    /// </summary>
    /// <remarks>The queue is initialized with the items from the provided collection, maintaining their
    /// order. The maximum size determines the capacity of the queue, and no more than <paramref name="maxSize"/>
    /// items can be added to it.</remarks>
    /// <param name="items">The collection of items to initialize the queue with. Must not be null.</param>
    /// <param name="maxSize">The maximum number of items the queue can hold. Must be greater than or equal to the number of items in
    /// <paramref name="items"/>.</param>
    public Dequeue(IEnumerable<T> items, int maxSize) {
        var (count, enumerable) = GetCountAndCollection(items);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxSize, count, nameof(maxSize));
        _buffer = new T[count];
        _firstIndex = 0;
        foreach (var item in enumerable) {
            AddLastInternal(item);
        }
        _maxSize = maxSize;
        _version = 0;
    }

    /// <summary>
    /// Gets the number of elements currently contained in the queue.
    /// </summary>
    public int Count => _count;

    /// <summary>
    /// Gets the total capacity of the queue.
    /// </summary>
    public int Capacity => _buffer.Length;

    /// <summary>
    /// Gets the maximum allowable size for the queue.
    /// </summary>
    public int MaxSize => _maxSize;

    /// <summary>
    /// Gets or sets the element at the specified index in the queue.
    /// </summary>
    /// <param name="index">The zero-based index of the element to get or set. Must be within the range of the queue.</param>
    /// <returns></returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="index"/> is less than 0 or greater than or equal to the number of elements in the
    /// queue.</exception>
    public T this[int index] {
        get {
            if ((uint)index < (uint)_count)
                return _buffer[(_firstIndex + index) % _buffer.Length];
            throw new ArgumentOutOfRangeException(nameof(index));
        }
        set {
            if ((uint)index < (uint)_count) {
                _buffer[(_firstIndex + index) % _buffer.Length] = value;
                _version += 1;
                return;
            }
            throw new ArgumentOutOfRangeException(nameof(index));
        }
    }

    /// <summary>
    /// Gets or sets the element at the specified index, supporting both forward and reverse indexing.
    /// </summary>
    /// <param name="index">The <see cref="Index"/> specifying the position of the element. If <see cref="Index.IsFromEnd"/> is <see
    /// langword="true"/>, the index is calculated from the end of the queue; otherwise, it is calculated from the start.</param>
    /// <returns></returns>
    public T this[Index index] {
        get => this[index.IsFromEnd ? _count - index.Value : index.Value];
        set => this[index.IsFromEnd ? _count - index.Value : index.Value] = value;
    }

    /// <summary>
    /// Gets the first element in the queue, or the default value for the type if the queue is empty.
    /// </summary>
    public T? First
        => _count == 0 ? default : _buffer[_firstIndex];

    /// <summary>
    /// Gets the last element in the queue, or the default value for the type if the queue is empty.
    /// </summary>
    public T? Last
        => _count == 0 ? default : _buffer[(_firstIndex + _count - 1) % _buffer.Length];

    /// <summary>
    /// Determines whether the queue contains the specified value using the provided equality comparer.
    /// </summary>
    /// <param name="value">The value to locate in the queue.</param>
    /// <param name="comparer">The equality comparer to use for comparing values.</param>
    /// <returns><see langword="true"/> if the specified value is found in the queue; otherwise, <see langword="false"/>.</returns>
    public bool Contains(T value, IEqualityComparer<T> comparer) {
        var index = _firstIndex;
        for (var i = 0; i < _count; i += 1) {
            if (comparer.Equals(value, _buffer[index])) {
                return true;
            }
            index = (index + i) % _buffer.Length;
        }
        return false;
    }

    /// <summary>
    /// Determines whether the queue contains the specified value.
    /// </summary>
    /// <remarks>This method uses the default equality comparer for the type <typeparamref name="T"/> to determine equality.</remarks>
    /// <param name="value">The value to locate in the queue.</param>
    /// <returns><see langword="true"/> if the specified value is found in the queue; otherwise, <see langword="false"/>.</returns>
    public bool Contains(T value)
        => Contains(value, EqualityComparer<T>.Default);

    public void Add(T value) => AddLast(value);

    /// <summary>
    /// Adds the specified value to the beginning of the queue.
    /// </summary>
    /// <remarks>This method increases the capacity of the queue if necessary to accommodate the new element.</remarks>
    /// <param name="value">The value to add to the queue.</param>
    public void AddFirst(T value) {
        EnsureCapacity(_count + 1);
        AddFirstInternal(value);
        _version += 1;
    }

    /// <summary>
    /// Adds the specified queue of values to the beginning of the queue.
    /// </summary>
    /// <remarks>The order of the values in the specified collection is preserved when they are added to the queue.</remarks>
    /// <param name="values">The collection of values to add. Cannot be <see langword="null"/>.</param>
    public void AddFirst(IEnumerable<T> values) {
        var (count, enumerable) = GetCountAndCollection(values);
        EnsureCapacity(_count + count);
        foreach (var value in enumerable) {
            AddFirstInternal(value);
        }
        _version += 1;
    }

    /// <summary>
    /// Adds a new element to the end of the queue.
    /// </summary>
    /// <remarks>This method increases the capacity of the queue if necessary to accommodate the new element.</remarks>
    /// <param name="value">The value to add to the end of the queue.</param>
    public void AddLast(T value) {
        EnsureCapacity(_count + 1);
        AddLastInternal(value);
        _version += 1;
    }

    /// <summary>
    /// Adds the elements of the specified collection to the end of the queue.
    /// </summary>
    /// <remarks>The method ensures sufficient capacity to accommodate the new elements before adding them.
    /// The order of elements in the specified collection is preserved when added to the queue.</remarks>
    /// <param name="values">The collection of elements to add. Cannot be null.</param>
    public void AddLast(IEnumerable<T> values) {
        var (count, enumerable) = GetCountAndCollection(values);
        EnsureCapacity(_count + count);
        foreach (var value in enumerable) {
            AddLastInternal(value);
        }
        _version += 1;
    }

    /// <summary>
    /// Attempts to remove and return the first element from the queue.
    /// </summary>
    /// <remarks>This method does not throw an exception if the queue is empty. Instead, it returns  <see
    /// langword="false"/> and sets <paramref name="value"/> to its default value.</remarks>
    /// <param name="value">When this method returns, contains the first element of the queue if the operation succeeds;  otherwise,
    /// the default value for the type of the queue. This parameter is passed uninitialized.</param>
    /// <returns><see langword="true"/> if the first element was successfully removed and returned;  otherwise, <see
    /// langword="false"/> if the queue is empty.</returns>
    public bool TryRemoveFirst([MaybeNullWhen(false)] out T value) {
        if (_count == 0) {
            value = default;
            return false;
        }
        value = _buffer[_firstIndex];
        _firstIndex = (_firstIndex + 1) % _buffer.Length;
        _count -= 1;
        _version += 1;
        return true;
    }

    /// <summary>
    /// Removes and returns the first element from the queue.
    /// </summary>
    /// <remarks>If the queue is empty, an <see cref="InvalidOperationException"/> is thrown.</remarks>
    /// <returns>The first element in the queue.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the queue is empty.</exception>
    public T RemoveFirst() {
        if (!TryRemoveFirst(out var value)) {
            throw new InvalidOperationException("Queue is empty");
        }
        return value;
    }

    /// <summary>
    /// Attempts to remove and return the last item in the queue.
    /// </summary>
    /// <remarks>This method does not throw an exception if the queue is empty. Instead, it returns <see langword="false"/>
    /// and sets <paramref name="value"/> to the default value for the type <typeparamref name="T"/>.</remarks>
    /// <param name="value">When this method returns, contains the last item in the queue if the operation was successful; otherwise,
    /// the default value for the type <typeparamref name="T"/>.</param>
    /// <returns><see langword="true"/> if the last item was successfully removed and returned; otherwise, <see
    /// langword="false"/> if the queue is empty.</returns>
    public bool TryRemoveLast([MaybeNullWhen(false)] out T value) {
        if (_count == 0) {
            value = default;
            return false;
        }
        value = _buffer[(_firstIndex + _count - 1) % _buffer.Length];
        _count -= 1;
        _version += 1;
        return true;
    }

    /// <summary>
    /// Removes and returns the last item from the queue.
    /// </summary>
    /// <remarks>If the queue is empty, an <see cref="InvalidOperationException"/> is thrown.</remarks>
    /// <returns>The last item in the queue.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the queue is empty.</exception>
    public T RemoveLast() {
        if (!TryRemoveLast(out var value)) {
            throw new InvalidOperationException("Queue is empty");
        }
        return value;
    }

    public IEnumerator<T> GetEnumerator() {
        var version = _version;
        var index = _firstIndex;
        for (var i = 0; i < _count; i += 1) {
            if (version != _version) {
                throw new InvalidOperationException("Collection was modified; enumeration operation can not continue");
            }
            yield return _buffer[index];
            index = (index + 1) % _buffer.Length;
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private void AddFirstInternal(T value) {
        if (_count > 0) {
            _firstIndex = (_firstIndex - 1 + _buffer.Length) % _buffer.Length;
        }
        _buffer[_firstIndex] = value;
        if (_count < _maxSize)
            _count += 1;
    }

    private void AddLastInternal(T value) {
        _buffer[(_firstIndex + _count) % _buffer.Length] = value;
        if (_count < _maxSize)
            _count += 1;
        else
            _firstIndex = (_firstIndex + 1) % _buffer.Length;
    }

    private void SetCapacity(int capacity) {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, _count, nameof(capacity));
        var newBuffer = new T[capacity];

        if (_firstIndex > _buffer.Length - _count) {
            Array.Copy(_buffer, _firstIndex, newBuffer, 0, _buffer.Length - _firstIndex);
            Array.Copy(_buffer, 0, newBuffer, _buffer.Length - _firstIndex, _count - _buffer.Length + _firstIndex);
        } else {
            Array.Copy(_buffer, _firstIndex, newBuffer, 0, _count);
        }
        _buffer = newBuffer;
        _firstIndex = 0;
    }

    private void EnsureCapacity(int capacity) {
        if (_buffer.Length < capacity && _buffer.Length < _maxSize) {
            capacity = (int)BitOperations.RoundUpToPowerOf2((uint)capacity);
            capacity = Math.Min(capacity, _maxSize);
            SetCapacity(capacity);
        }
    }

    private static (int, IEnumerable<T>) GetCountAndCollection(IEnumerable<T> source) {
        if (source is IReadOnlyCollection<T> readonlyCollection)
            return (readonlyCollection.Count, source);
        if (source is ICollection<T> genericCollection)
            return (genericCollection.Count, source);
        if (source is ICollection collection)
            return (collection.Count, source);
        var array = source.ToArray();
        return (array.Length, array);
    }
}

public class DequeueBuilder {
    public static Dequeue<T> Create<T>(ReadOnlySpan<T> items) {
        var queue = new Dequeue<T>(items.Length);
        foreach (var item in items) {
            queue.AddLast(item);
        }
        return queue;
    }
}
