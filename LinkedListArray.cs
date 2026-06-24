using System.Collections;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace RvB.Collections;

/// <summary>
/// Represents a data structure that combines the features of a linked list and an array, providing efficient insertion,
/// deletion, and traversal operations while maintaining a fixed-size or dynamically resizable internal storage.
/// </summary>
/// <remarks>This class is designed to offer the flexibility of a linked list with the memory efficiency of an
/// array. It supports operations such as adding elements to the beginning or end of the list, inserting elements before
/// or after specific nodes, and removing elements. The internal storage is dynamically resized as needed to accommodate
/// new elements. <para> The <see cref="LinkedListArray{T}"/> maintains a circular doubly linked list of active nodes
/// and a singly linked list of freed nodes for efficient reuse of storage. </para> <para> This class is not
/// thread-safe. If multiple threads access an instance of <see cref="LinkedListArray{T}"/> concurrently, it is the
/// caller's responsibility to synchronize access. </para></remarks>
/// <typeparam name="T">The type of elements stored in the <see cref="LinkedListArray{T}"/>.</typeparam>
[DebuggerDisplay("Count = {Count}, Capacity = {Capacity}")]
[CollectionBuilder(typeof(LinkedListArrayBuilder), nameof(LinkedListArrayBuilder.Create))]
public sealed class LinkedListArray<T> : ICollection<T>, IReadOnlyCollection<T> {
    private const int NoIndex = -1;
    private const int MinSize = 16;

    public const uint NoId = uint.MaxValue;

    /// <summary>
    /// Represents the internal storage for the data structure, where each element contains the next index, the
    /// previous index, and the value of type <typeparamref name="T"/>.
    /// </summary>
    private (int Next, int Prev, T? Value)[] _storage;
    /// <summary>
    /// Represents the index of the head node in a doubly linked, circular list of used nodes.
    /// A value of -1 indicates that there are no used nodes.
    /// </summary>
    private int _headIndex;
    /// <summary>
    /// Represents the index of the first node in a non-circular singly linked list of freed nodes.
    /// Nodes that are part of this list have a Prev of -1 and the last node has a Next of -1.
    /// A value of -1 indicates that there are no freed nodes available.
    /// </summary>
    private int _freeIndex;
    /// <summary>
    /// Represents the starting index of unclaimed space in <see cref="_storage"/>.
    /// </summary>
    private uint _unusedIndex;
    /// <summary>
    /// Represents the count of used nodes in <see cref="_storage"/>.
    /// </summary>
    private int _count;
    private int _version;

    private readonly IEqualityComparer<T> _comparer;

    /// <summary>
    /// Initializes a new instance of the <see cref="LinkedListArray{T}"/> class with a default initial capacity of 16.
    /// </summary>
    /// <remarks>This constructor creates a <see cref="LinkedListArray{T}"/> with a predefined capacity, allowing
    /// for efficient storage of up to 16 elements before resizing is required. For a custom initial capacity, use the
    /// constructor that accepts a capacity parameter.</remarks>
    public LinkedListArray() : this(MinSize, null) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="LinkedListArray{T}"/> class with the specified capacity and an
    /// optional equality comparer.
    /// </summary>
    /// <param name="capacity">The initial capacity of the linked list array. Must be a non-negative value.</param>
    /// <param name="comparer">An optional equality comparer to use for comparing elements. If <see langword="null"/>, the default equality
    /// comparer for the type <typeparamref name="T"/> is used.</param>
    public LinkedListArray(int capacity, IEqualityComparer<T>? comparer = null) {
        ArgumentOutOfRangeException.ThrowIfNegative(capacity, nameof(capacity));
        _storage = new (int Next, int Prev, T? Value)[capacity];
        _unusedIndex = 0;
        _headIndex = NoIndex;
        _freeIndex = NoIndex;
        _count = 0;
        _version = 0;
        _comparer = comparer ?? EqualityComparer<T>.Default;
    }

    public LinkedListArray(IEnumerable<T> source, IEqualityComparer<T>? comparer = null) {
        var capacity = MinSize;
        if (source is ICollection<T> collection) {
            capacity = int.Max(capacity, collection.Count);
        } else if (source is IReadOnlyCollection<T> roCollection) {
            capacity = int.Max(capacity, roCollection.Count);
        } else if (source is T[] array) {
            capacity = int.Max(capacity, array.Length);
        }
        _storage = new (int Next, int Prev, T? Value)[capacity];
        _unusedIndex = 0;
        _headIndex = NoIndex;
        _freeIndex = NoIndex;
        _count = 0;
        _version = 0;
        _comparer = comparer ?? EqualityComparer<T>.Default;
        foreach (var item in source) {
            AddLast(item);
        }
    }

    /// <summary>
    /// Gets the number of elements currently stored in the collection.
    /// </summary>
    public int Count => _count;

    /// <summary>
    /// Gets the total number of elements that the underlying storage can hold without resizing.
    /// </summary>
    public int Capacity => _storage.Length;

    public (uint Id, T? Value) First {
        get {
            if (_headIndex == NoIndex) {
                return (NoId, default);
            }
            return ((uint)_headIndex, _storage[_headIndex].Value);
        }
    }

    public (uint Id, T? Value) Last {
        get {
            if (_headIndex == NoIndex) {
                return (NoId, default);
            }
            uint tailIndex = (uint)_storage[_headIndex].Prev;
            return (tailIndex, _storage[tailIndex].Value);
        }
    }

    /// <summary>
    /// Adds the specified value to the end of the collection.
    /// </summary>
    /// <remarks>This method appends the specified value to the end of the collection.</remarks>
    /// <param name="value">The value to add to the collection.</param>
    void ICollection<T>.Add(T value) => AddLast(value);

    /// <summary>
    /// Determines whether the collection contains a specific value.
    /// </summary>
    /// <param name="value">The value to locate in the collection.</param>
    /// <returns><see langword="true"/> if the specified value is found in the collection; otherwise, <see langword="false"/>.</returns>
    bool ICollection<T>.Contains(T value) => FindFirstIndex(value) != NoIndex;

    /// <summary>
    /// Copies the elements of the collection to a specified array, starting at the specified array index.
    /// </summary>
    /// <remarks>This method copies the elements of the collection in the order they are stored. If the
    /// collection is empty, no elements are copied.</remarks>
    /// <param name="array">The one-dimensional array that is the destination of the elements copied from the collection. The array must
    /// have zero-based indexing.</param>
    /// <param name="arrayIndex">The zero-based index in the destination array at which copying begins.</param>
    /// <exception cref="ArgumentException">Thrown if the number of elements in the collection is greater than the available space from <paramref
    /// name="arrayIndex"/> to the end of the destination <paramref name="array"/>.</exception>
    void ICollection<T>.CopyTo(T[] array, int arrayIndex) {
        ArgumentNullException.ThrowIfNull(array);
        ArgumentOutOfRangeException.ThrowIfNegative(arrayIndex);

        if (arrayIndex + _count > array.Length)
            throw new ArgumentException("Not enough space in array");
        if (_headIndex == NoIndex)
            return;
        var index = _headIndex;
        do {
            var (next, _, value) = _storage[index];
            array[arrayIndex++] = value!;
            index = next;
        } while (index != _headIndex);
    }

    /// <summary>
    /// Gets a value indicating whether the collection is read-only.
    /// </summary>
    bool ICollection<T>.IsReadOnly => false;

    /// <summary>
    /// Adds the specified value to the end of the collection.
    /// </summary>
    /// <remarks>If the collection is empty, the added element becomes the first element.</remarks>
    /// <param name="value">The value to add to the end of the collection.</param>
    public uint AddLast(T value) {
        var index = AddNode(value, _headIndex, NoIndex);
        if (_headIndex == NoIndex)
            _headIndex = index;
        return (uint)index;
    }

    /// <summary>
    /// Adds the specified value to the beginning of the collection.
    /// </summary>
    /// <remarks>This method updates the head of the collection to point to the newly added element.</remarks>
    /// <param name="value">The value to add to the collection.</param>
    public uint AddFirst(T value) {
        _headIndex = AddNode(value, _headIndex, NoIndex);
        return (uint)_headIndex;
    }

    /// <summary>
    /// Inserts the specified value into the linked list immediately before the specified node.
    /// </summary>
    /// <remarks>If the specified <paramref name="id"/> is the head of the linked list, the new node becomes
    /// the new head.</remarks>
    /// <param name="id">The identifier before which the new value will be inserted. Must be a valid identifier in the linked list.</param>
    /// <param name="value">The value to store in the new node.</param>
    /// <returns>An identifier representing the newly added node.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the specified <paramref name="id"/> is not part of the linked list.</exception>
    public uint AddBefore(uint id, T value) {
        if (id < _unusedIndex) {
            var prev = _storage[id].Prev;
            if (prev != NoIndex) {
                var index = AddNode(value, (int)id, prev);
                if (_headIndex == id)
                    _headIndex = index;
                return (uint)index;
            }
        }
        throw new ArgumentOutOfRangeException(nameof(id));
    }

    /// <summary>
    /// Inserts the specified value into the linked list immediately after the specified node.
    /// </summary>
    /// <param name="id">The identifier after which the new value will be added. Must be a valid identifier in the linked list.</param>
    /// <param name="value">The value to store in the new node.</param>
    /// <returns>An identifier representing the newly added node.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the specified <paramref name="id"/> is not part of the linked list.</exception>
    public uint AddAfter(uint id, T value) {
        if (id < _unusedIndex) {
            var (next, prev, _) = _storage[id];
            if (prev != NoIndex) {
                return (uint)AddNode(value, next, (int)id);
            }
        }
        throw new ArgumentOutOfRangeException(nameof(id));
    }

    /// <summary>
    /// Removes the first occurrence of the specified value from the collection.
    /// </summary>
    /// <param name="value">The value to remove from the collection.</param>
    /// <returns><see langword="true"/> if the value was successfully removed; otherwise, <see langword="false"/>.</returns>
    public bool Remove(T value) {
        var index = FindFirstIndex(value);
        if (index == NoIndex)
            return false;
        return InternalRemove(index);
    }

    /// <summary>
    /// Removes the specified node from the linked list.
    /// </summary>
    /// <param name="id">The identifier of the node to remove from the list.</param>
    /// <returns><see langword="true"/> if the node was successfully removed; otherwise, <see langword="false"/>.</returns>
    public bool RemoveById(uint id) => InternalRemove((int)id);

    /// <summary>
    /// Removes the first element from the collection.
    /// </summary>
    /// <returns><see langword="true"/> if the first element was successfully removed;  otherwise, <see langword="false"/>.</returns>
    public bool RemoveFirst() => InternalRemove(_headIndex);

    /// <summary>
    /// Removes the last element from the collection.
    /// </summary>
    /// <returns><see langword="true"/> if the last element was successfully removed;  otherwise, <see langword="false"/>.</returns>
    public bool RemoveLast() {
        if (_headIndex == NoIndex)
            return false;
        return InternalRemove(_storage[_headIndex].Prev);
    }

    /// <summary>
    /// Clears all elements from the collection, resetting it to its initial state.
    /// </summary>
    /// <remarks>After calling this method, the collection will be empty.</remarks>
    public void Clear() {
        _headIndex = NoIndex;
        _freeIndex = NoIndex;
        _unusedIndex = 0;
        _count = 0;
        _version++;
    }

    /// <summary>
    /// Returns an enumerator that iterates through the collection.
    /// </summary>
    /// <remarks>The enumerator provides a snapshot of the collection at the time it is created.  If the
    /// collection is modified after the enumerator is created, an  <see cref="InvalidOperationException"/> is thrown
    /// during enumeration.</remarks>
    /// <returns>An <see cref="IEnumerator{T}"/> that can be used to iterate through the collection.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the collection is modified after the enumerator is created.</exception>
    public Enumerator GetEnumerator() => new Enumerator(this);

    /// <inheritdoc cref="GetEnumerator"/>
    IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();

    /// <inheritdoc cref="GetEnumerator"/>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// Retrieves the next node in the linked list relative to the specified node.
    /// </summary>
    /// <param name="id">The identifier of the node for which the next node is to be retrieved.</param>
    /// <returns>A tuple of identifier and value representing the next node in the linked list.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if the specified <paramref name="id"/> is invalid.</exception>
    public (uint Id, T Value) Next(uint id) {
        if (id < _unusedIndex) {
            var (next, prev, _) = _storage[id];
            if (prev != NoIndex) {
                return ((uint)next, _storage[next].Value!);
            }
        }
        throw new ArgumentOutOfRangeException(nameof(id));
    }

    /// <summary>
    /// Retrieves the previous node in the linked list relative to the specified node.
    /// </summary>
    /// <param name="id">The identifier of the node for which the previous node is to be retrieved.</param>
    /// <returns>A tuple of identifier and value representing the previous node in the linked list.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if the specified <paramref name="id"/> is invalid.</exception>
    public (uint Id, T Value) Prev(uint id) {
        if (id < _unusedIndex) {
            var prev = _storage[id].Prev;
            if (prev != NoIndex) {
                return ((uint)prev, _storage[prev].Value!);
            }
        }
        throw new ArgumentOutOfRangeException(nameof(id));
    }

    /// <summary>
    /// Gets the identifier and value at the specified index in the linked list.
    /// </summary>
    /// <param name="index">The zero-based index of the node to retrieve. Must be greater than or equal to 0 and less than the total number
    /// of nodes in the list.</param>
    /// <returns>A tuple of identifier and value representing the node at the specified index.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="index"/> is less than 0 or greater than or equal to the number of nodes in the list.</exception>
    public (uint Id, T Value) this[int index] {
        get {
            if ((uint)index < (uint)_count) {
                var i = _headIndex;
                while (index-- > 0) {
                    i = _storage[i].Next;
                }
                return ((uint)i, _storage[i].Value!);
            }
            throw new ArgumentOutOfRangeException(nameof(index));
        }
    }

    /// <summary>
    /// Retrieves the value associated with the specified identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the value to retrieve.</param>
    /// <returns>The value associated with the specified <paramref name="id"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if the specified <paramref name="id"/> is invalid.</exception>
    public T GetById(uint id) {
        if (id < _unusedIndex) {
            var (_, prev, value) = _storage[id];
            if (prev != NoIndex) {
                return value!;
            }
        }
        throw new ArgumentOutOfRangeException(nameof(id));
    }

    /// <inheritdoc cref="FindFirst(T)"/>
    public uint Find(T value) => FindFirst(value);

    /// <summary>
    /// Finds the first occurrence of the specified value in the linked list.
    /// </summary>
    /// <remarks>The search is performed in the order of the linked list. If the value is found, it returns
    /// the identifier of the first match.</remarks>
    /// <param name="value">The value to locate in the linked list.</param>
    /// <returns>An identifier representing the first occurrence of the specified value, or <see cref="NoId"/> if the value is not found.</returns>
    public uint FindFirst(T value) {
        var index = FindFirstIndex(value);
        if (index == NoIndex) {
            return NoId;
        }
        return (uint)index;
    }

    /// <summary>
    /// Finds the last occurrence of the specified value in the linked list.
    /// </summary>
    /// <remarks>The search is performed from the end to the beginning in the linked list. If the value is found, it returns
    /// the identifier of the first match.</remarks>
    /// <param name="value">The value to locate in the linked list.</param>
    /// <returns>An identifier representing the first occurrence of the specified value, or <see cref="NoId"/> if the value is not found.</returns>
    public uint FindLast(T value) {
        var index = FindLastIndex(value);
        if (index == NoIndex) {
            return NoId;
        }
        return (uint)index;
    }

    /// <summary>
    /// Compacts the internal storage by reducing unused capacity while optionally reserving a percentage of free space.
    /// </summary>
    /// <remarks>This method reorganizes the internal storage to minimize unused space and improve memory
    /// efficiency.  If <paramref name="percentageFree"/> is greater than 0, the method ensures that the specified
    /// percentage of free space is reserved relative to the current number of elements. If the internal storage is
    /// already  optimized or empty, no action is taken.</remarks>
    /// <param name="percentageFree">The percentage of additional free space to reserve after compaction.</param>
    public void Compact(uint percentageFree = 0) {
        if (_headIndex == NoIndex) {
            Clear();
            if (_storage.Length > 0)
                _storage = [];
            return;
        }
        var absoluteFree = 0u;
        if (percentageFree > 0) {
            absoluteFree = uint.Max(2u, (percentageFree * (uint)_count) / 100u);
        }
        var threshold = (uint)_count + absoluteFree;
        if (_unusedIndex > threshold) {
            Debug.Assert(_freeIndex != NoIndex);
            var nodeIndex = _headIndex;

            do {
                if (nodeIndex >= threshold) {
                    var freeNodeIndex = _freeIndex;
                    while (freeNodeIndex >= threshold) {
                        var nextFreeNodeIndex = _storage[freeNodeIndex].Next;
#if DEBUG               
                        _storage[freeNodeIndex] = default;
#endif
                        freeNodeIndex = nextFreeNodeIndex;
                    }
                    ref var freeNode = ref _storage[freeNodeIndex];
                    ref var node = ref _storage[nodeIndex];
                    _freeIndex = freeNode.Next;
                    freeNode = node;
                    _storage[node.Next].Prev = freeNodeIndex;
                    _storage[node.Prev].Next = freeNodeIndex;
                    if (nodeIndex == _headIndex) {
                        _headIndex = freeNodeIndex;
                    }
                    nodeIndex = freeNodeIndex;
#if DEBUG
                    node = default;
#endif
                }
                nodeIndex = _storage[nodeIndex].Next;
            } while (nodeIndex != _headIndex);
            _unusedIndex = threshold;

            var storage = new (int Next, int Prev, T? Value)[threshold];
            Array.Copy(_storage, storage, _unusedIndex);
            _storage = storage;
        }
    }

    private int FindFirstIndex(T value) {
        if (_headIndex == NoIndex) {
            return NoIndex;
        }
        var index = _headIndex;
        var comparer = _comparer;
        do {
            var (next, _, v) = _storage[index];
            if (comparer.Equals(v, value)) {
                return index;
            }
            index = next;
        } while (index != _headIndex);
        return NoIndex;
    }

    private int FindLastIndex(T value) {
        if (_headIndex == NoIndex) {
            return NoIndex;
        }
        var tail = _storage[_headIndex].Prev;
        var index = tail;
        var comparer = _comparer;
        do {
            var (_, prev, v) = _storage[index];
            if (comparer.Equals(v, value)) {
                return index;
            }
            index = prev;
        } while (index != tail);
        return NoIndex;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int AllocateFreeIndex() {
        if (_freeIndex != NoIndex) {
            var free = _freeIndex;
            _freeIndex = _storage[_freeIndex].Next;
            return free;
        }
        if (_unusedIndex == _storage.Length) {
            Debug.Assert(_count == _storage.Length);
            // Make more space
            var length = int.Max(4, _storage.Length * 2);
            var storage = new (int Next, int Prev, T? Value)[length];
            Array.Copy(_storage, storage, _storage.Length);
            _storage = storage;
        }
        return (int)_unusedIndex++;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int AddNode(T value, int next, int prev) {
        var index = AllocateFreeIndex();
        if (_headIndex == NoIndex) {
            _storage[index] = (index, index, value);
            _headIndex = index;
        } else {
            ref var nextNode = ref _storage[next];
            if (prev == NoIndex) {
                prev = nextNode.Prev;
            }
            _storage[index] = (next, prev, value);
            _storage[prev].Next = index;
            nextNode.Prev = index;
        }
        _count += 1;
        _version++;
        return index;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool InternalRemove(int index) {
        if (_headIndex == NoIndex)
            return false;
        ref var node = ref _storage[index];
        var prev = node.Prev;
        if (prev == NoIndex)
            // The node is part of the free list
            return false;
        if (--_count == 0) {
            _headIndex = NoIndex;
        } else {
            var next = node.Next;
            _storage[prev].Next = next;
            _storage[next].Prev = prev;
            if (index == _headIndex)
                _headIndex = next;
        }
        // Add it to the start of free linked list
        if (_freeIndex == NoIndex) {
            _freeIndex = index;
            node = (NoIndex, NoIndex, default);
        } else {
            node = (_freeIndex, NoIndex, default);
            _freeIndex = index;
        }
        _version++;
        return true;
    }

#if DEBUG
    void CheckConsistency() {
        int free = 0;
        var index = _freeIndex;
        while (index != NoIndex) {
            if ((uint)index >= (uint)_unusedIndex)
                throw new InvalidDataException();
            if (_storage[index].Prev != NoIndex)
                throw new InvalidDataException();
            free += 1;
            index = _storage[index].Next;
        }
        index = _headIndex;
        var count = 0;
        if (index != NoIndex) {
            do {
                if ((uint)index >= (uint)_unusedIndex)
                    throw new InvalidDataException();
                if ((uint)_storage[index].Prev >= (uint)_unusedIndex)
                    throw new InvalidDataException();
                index = _storage[index].Next;
                count += 1;
            } while (index != _headIndex);
        }
        if (count != _count)
            throw new InvalidDataException();
        if (count + free != _unusedIndex)
            throw new InvalidDataException();
    }
#endif

    public struct Enumerator : IEnumerator<T> {
        private readonly LinkedListArray<T> _list;
        private readonly int _version;
        private T _current;
        private int _nextIndex;
        private bool _finished;

        public Enumerator(LinkedListArray<T> list) : this() {
            _list = list;
            _version = list._version;
            _nextIndex = list._headIndex;
            _finished = _nextIndex == NoIndex;
            _current = default!;
        }

        public readonly T Current => _current;

        readonly object IEnumerator.Current => _current!;

        public bool MoveNext() {
            if (!_finished) {
                if (_list._version != _version) {
                    throw new InvalidOperationException("Collection was modified; enumeration operation can not continue");
                }
                (_nextIndex, _, var value) = _list._storage[_nextIndex];
                _current = value!;
                _finished = _nextIndex == _list._headIndex;
                return true;
            }
            return false;
        }

        public void Reset() {
            _nextIndex = _list._headIndex;
            _finished = _nextIndex == NoIndex;
            _current = default!;
        }

        public readonly void Dispose() { }
    }
}

public class LinkedListArrayBuilder {
    public static LinkedListArray<T> Create<T>(ReadOnlySpan<T> items) {
        var list = new LinkedListArray<T>(items.Length);
        foreach (var item in items) {
            list.AddLast(item);
        }
        return list;
    }
}
