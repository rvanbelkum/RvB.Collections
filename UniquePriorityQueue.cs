using System.Collections;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace RvB.Collections;

/// <summary>
///  Represents a min priority queue.
/// </summary>
/// <typeparam name="TElement">Specifies the type of elements in the queue.</typeparam>
/// <typeparam name="TPriority">Specifies the type of priority associated with enqueued elements.</typeparam>
/// <remarks>
///  Implements an array-backed quaternary min-heap. Each element is enqueued with an associated priority
///  that determines the dequeue order: elements with the lowest priority get dequeued first.
/// </remarks>
[DebuggerDisplay("Count = {Count}")]
public class UniquePriorityQueue<TElement, TPriority> where TElement : notnull {
    /// <summary>
    /// Represents an implicit heap-ordered complete d-ary tree, stored as an array.
    /// </summary>
    private (TElement Element, TPriority Priority)[] _nodes;

    private readonly Dictionary<TElement, int> _elementMap;

#if DEBUG
    private int _duplicateCount;
    private int _higherPriorityCount;
#endif

    /// <summary>
    /// Custom comparer used to order the heap.
    /// </summary>
    private readonly IComparer<TPriority>? _priorityComparer;

    private readonly IEqualityComparer<TElement>? _elementComparer;

    /// <summary>
    /// The number of nodes in the heap.
    /// </summary>
    private int _size;

    /// <summary>
    /// Version updated on mutation to help validate enumerators operate on a consistent state.
    /// </summary>
    private int _version;

    /// <summary>
    /// Specifies the arity of the d-ary heap, which here is quaternary.
    /// It is assumed that this value is a power of 2.
    /// </summary>
    private const int Arity = 4;

    /// <summary>
    /// The binary logarithm of <see cref="Arity" />.
    /// </summary>
    private const int Log2Arity = 2;

#if DEBUG
    static UniquePriorityQueue() {
        Debug.Assert(Log2Arity > 0 && Math.Pow(2, Log2Arity) == Arity);
    }
#endif

    /// <summary>
    ///  Initializes a new instance of the <see cref="UniquePriorityQueue{TElement, TPriority}"/> class.
    /// </summary>
    public UniquePriorityQueue() : this(priorityComparer: null, elementComparer: null) { }

    /// <summary>
    ///  Initializes a new instance of the <see cref="UniquePriorityQueue{TElement, TPriority}"/> class
    ///  with the specified initial capacity.
    /// </summary>
    /// <param name="initialCapacity">Initial capacity to allocate in the underlying heap array.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    ///  The specified <paramref name="initialCapacity"/> was negative.
    /// </exception>
    public UniquePriorityQueue(int initialCapacity)
        : this(initialCapacity, comparer: null, equater: null) { }

    public UniquePriorityQueue(int initialCapacity, IEqualityComparer<TElement>? equater)
        : this(initialCapacity, comparer: null, equater) { }

    /// <summary>
    ///  Initializes a new instance of the <see cref="UniquePriorityQueue{TElement, TPriority}"/> class
    ///  with the specified custom priority comparer.
    /// </summary>
    /// <param name="comparer">Custom comparer dictating the ordering of elements.
    ///  Uses <see cref="Comparer{T}.Default" /> if the argument is <see langword="null"/>.
    /// </param>
    public UniquePriorityQueue(IComparer<TPriority>? comparer)
        : this(comparer, elementComparer: null) { }

    /// <summary>
    ///  Initializes a new instance of the <see cref="UniquePriorityQueue{TElement, TPriority}"/> class
    ///  using the specified equality comparer to determine element uniqueness.
    /// </summary>
    /// <remarks>Use this constructor to provide custom equality logic for elements, which is useful when
    /// working with complex types or when the default equality comparison does not meet your requirements.</remarks>
    /// <param name="equater">The equality comparer used to determine whether elements in the queue are considered equal. If null, the default
    /// equality comparer for type TElement is used.</param>
    public UniquePriorityQueue(IEqualityComparer<TElement>? equater)
        : this(priorityComparer: null, equater) { }

    /// <summary>
    ///  Initializes a new instance of the <see cref="UniquePriorityQueue{TElement, TPriority}"/> class
    ///  the specified custom priority comparer and equality comparer to determine element uniqueness.
    /// </summary>
    /// <remarks>Use this constructor to provide custom priority ordering and custom equality logic for elements, which is useful when
    ///  working with complex types or when the default priority and equality comparison does not meet your requirements.</remarks>
    /// <param name="priorityComparer">
    ///  Custom comparer dictating the ordering of elements.
    ///  Uses <see cref="Comparer{T}.Default" /> if the argument is <see langword="null"/>.</param>
    /// <param name="elementComparer">The equality comparer used to determine whether elements in the queue are considered equal. If null, the default
    ///  equality comparer for type TElement is used.</param>
    public UniquePriorityQueue(IComparer<TPriority>? priorityComparer, IEqualityComparer<TElement>? elementComparer) {
        _nodes = [];
        _elementComparer = InitializeEquater(elementComparer);
        _priorityComparer = InitializeComparer(priorityComparer);
        _elementMap = new(_elementComparer);
    }

    /// <summary>
    ///  Initializes a new instance of the <see cref="UniquePriorityQueue{TElement, TPriority}"/> class
    ///  with the specified initial capacity and custom priority comparer.
    /// </summary>
    /// <param name="initialCapacity">Initial capacity to allocate in the underlying heap array.</param>
    /// <param name="comparer">
    ///  Custom comparer dictating the ordering of elements.
    ///  Uses <see cref="Comparer{T}.Default" /> if the argument is <see langword="null"/>.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    ///  The specified <paramref name="initialCapacity"/> was negative.
    /// </exception>
    public UniquePriorityQueue(int initialCapacity, IComparer<TPriority>? comparer)
        : this(initialCapacity, comparer, equater: null) { }

    public UniquePriorityQueue(int initialCapacity, IComparer<TPriority>? comparer, IEqualityComparer<TElement>? equater) {
        ArgumentOutOfRangeException.ThrowIfNegative(initialCapacity);

        _nodes = new (TElement, TPriority)[initialCapacity];
        _priorityComparer = InitializeComparer(comparer);
        _elementComparer = InitializeEquater(equater);
        _elementMap = new(initialCapacity, _elementComparer);
    }

    /// <summary>
    ///  Initializes a new instance of the <see cref="UniquePriorityQueue{TElement, TPriority}"/> class
    ///  that is populated with the specified elements and priorities.
    /// </summary>
    /// <param name="items">The pairs of elements and priorities with which to populate the queue.</param>
    /// <exception cref="ArgumentNullException">
    ///  The specified <paramref name="items"/> argument was <see langword="null"/>.
    /// </exception>
    /// <remarks>
    ///  Constructs the heap using a heapify operation,
    ///  which is generally faster than enqueuing individual elements sequentially.
    /// </remarks>
    public UniquePriorityQueue(IEnumerable<(TElement Element, TPriority Priority)> items)
        : this(items, comparer: null, equater: null) { }

    public UniquePriorityQueue(IEnumerable<(TElement Element, TPriority Priority)> items, IEqualityComparer<TElement>? equater)
        : this(items, comparer: null, equater) { }

    /// <summary>
    ///  Initializes a new instance of the <see cref="UniquePriorityQueue{TElement, TPriority}"/> class
    ///  that is populated with the specified elements and priorities,
    ///  and with the specified custom priority comparer.
    /// </summary>
    /// <param name="items">The pairs of elements and priorities with which to populate the queue.</param>
    /// <param name="comparer">
    ///  Custom comparer dictating the ordering of elements.
    ///  Uses <see cref="Comparer{T}.Default" /> if the argument is <see langword="null"/>.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///  The specified <paramref name="items"/> argument was <see langword="null"/>.
    /// </exception>
    /// <remarks>
    ///  Constructs the heap using a heapify operation,
    ///  which is generally faster than enqueuing individual elements sequentially.
    /// </remarks>
    public UniquePriorityQueue(IEnumerable<(TElement Element, TPriority Priority)> items, IComparer<TPriority>? comparer)
        : this(items, comparer, equater: null) { }

    public UniquePriorityQueue(IEnumerable<(TElement Element, TPriority Priority)> items, IComparer<TPriority>? comparer, IEqualityComparer<TElement>? equater) {
        ArgumentNullException.ThrowIfNull(items);

        _nodes = EnumerableHelpers.ToArray(items, out _size);
        _priorityComparer = InitializeComparer(comparer);
        _elementComparer = InitializeEquater(equater);
        _elementMap = new(_size, _elementComparer);
        if (_size > 0) {
            var elementMap = _elementMap;
            var nodes = _nodes;
            for (var i = _size - 1; i >= 0; i -= 1) {
                elementMap[nodes[i].Element] = i;
            }
        }
        if (_size > 1) {
            Heapify();
        }
    }

    /// <summary>
    ///  Gets the number of elements contained in the <see cref="UniquePriorityQueue{TElement, TPriority}"/>.
    /// </summary>
    public int Count => _size;

    /// <summary>
    ///  Gets the total numbers of elements the queue's backing storage can hold without resizing.
    /// </summary>
    public int Capacity => _nodes.Length;

    /// <summary>
    ///  Gets the priority comparer used by the <see cref="UniquePriorityQueue{TElement, TPriority}"/>.
    /// </summary>
    public IComparer<TPriority> Comparer => _priorityComparer ?? Comparer<TPriority>.Default;

    /// <summary>
    ///  Gets a collection that enumerates the elements of the queue in an unordered manner.
    /// </summary>
    /// <remarks>
    ///  The enumeration does not order items by priority, since that would require N * log(N) time and N space.
    ///  Items are instead enumerated following the internal array heap layout.
    /// </remarks>
#if NET10_0_OR_GREATER
    public UnorderedItemsCollection UnorderedItems => field ??= new UnorderedItemsCollection(this);
#else
    private UnorderedItemsCollection? _unorderedItemsCollection = null;
    public UnorderedItemsCollection UnorderedItems => _unorderedItemsCollection ??= new UnorderedItemsCollection(this);
#endif

    /// <summary>
    /// Adds the specified element to the queue with the given priority, or updates its priority if the element already
    /// exists and the new priority is higher.
    /// </summary>
    /// <remarks>If the element is already present in the queue, its priority is updated only if the new
    /// priority is higher than the current one. The queue automatically increases its capacity if needed.</remarks>
    /// <param name="element">The element to add to the queue. Cannot be null.</param>
    /// <param name="priority">The priority to associate with the element. Determines the order in which elements are processed.</param>
    /// <returns><see langword="true"/> if the element was added to the queue or its priority was updated; otherwise, <see langword="false"/>
    /// if the element already exists with an equal or higher priority.</returns>
    public bool Enqueue(TElement element, TPriority priority) {
        if (_elementMap.TryGetValue(element, out var index)) {
#if DEBUG
            _duplicateCount++;
#endif
            var nodePriority = _nodes[index].Priority;
            if (_priorityComparer == null) {
                if (Comparer<TPriority>.Default.Compare(priority, nodePriority) < 0) {
                    _nodes[index].Element = element;
                    MoveUpDefaultComparer((element, priority), index);
                    _version++;
#if DEBUG
                    _higherPriorityCount++;
#endif
                    return true;
                }
            } else {
                if (_priorityComparer.Compare(priority, nodePriority) < 0) {
                    _nodes[index].Element = element;
                    MoveUpCustomComparer((element, priority), index);
                    _version++;
#if DEBUG
                    _higherPriorityCount++;
#endif
                    return true;
                }
            }
            return false;
        }
        // Virtually add the node at the end of the underlying array.
        // Note that the node being enqueued does not need to be physically placed
        // there at this point, as such an assignment would be redundant.
        int currentSize = _size;
        _version++;

        if (_nodes.Length == currentSize) {
            Grow(currentSize + 1);
        }
        _size = currentSize + 1;
        if (_priorityComparer == null) {
            MoveUpDefaultComparer((element, priority), currentSize);
        } else {
            MoveUpCustomComparer((element, priority), currentSize);
        }
        return true;
    }

    /// <summary>
    ///  Returns the minimal element from the <see cref="UniquePriorityQueue{TElement, TPriority}"/> without removing it.
    /// </summary>
    /// <exception cref="InvalidOperationException">The <see cref="UniquePriorityQueue{TElement, TPriority}"/> is empty.</exception>
    /// <returns>The minimal element of the <see cref="UniquePriorityQueue{TElement, TPriority}"/>.</returns>
    public TElement Peek() {
        if (_size == 0) {
            throw new InvalidOperationException("Queue is empty");
        }
        return _nodes[0].Element;
    }

    /// <summary>
    ///  Removes and returns the minimal element from the <see cref="UniquePriorityQueue{TElement, TPriority}"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">The queue is empty.</exception>
    /// <returns>The minimal element of the <see cref="UniquePriorityQueue{TElement, TPriority}"/>.</returns>
    public TElement Dequeue() {
        if (!TryDequeue(out var element, out _)) {
            throw new InvalidOperationException("Queue is empty");
        }
        return element;
    }

    /// <summary>
    ///  Removes the minimal element and then immediately adds the specified element with associated priority to the <see cref="UniquePriorityQueue{TElement, TPriority}"/>,
    /// </summary>
    /// <param name="element">The element to add to the <see cref="UniquePriorityQueue{TElement, TPriority}"/>.</param>
    /// <param name="priority">The priority with which to associate the new element.</param>
    /// <exception cref="InvalidOperationException">The queue is empty.</exception>
    /// <returns>The minimal element removed before performing the enqueue operation.</returns>
    /// <remarks>
    ///  Implements an extract-then-insert heap operation that is generally more efficient
    ///  than sequencing Dequeue and Enqueue operations: in the worst case scenario only one
    ///  shift-down operation is required.
    /// </remarks>
    public TElement DequeueEnqueue(TElement element, TPriority priority) {
        if (_size == 0) {
            throw new InvalidOperationException("Queue is empty");
        }
        var (rootElement, rootPriority) = _nodes[0];
        var elementNodeIndices = _elementMap;

        elementNodeIndices.Remove(rootElement);
        if (elementNodeIndices.ContainsKey(element)) {
            elementNodeIndices[rootElement] = 0; // Add it back in.
            throw new ArgumentException("An element with the same value already exists in the queue.", nameof(element));
        }
        if (_priorityComparer == null) {
            if (Comparer<TPriority>.Default.Compare(priority, rootPriority) > 0) {
                MoveDownDefaultComparer((element, priority), 0);
            } else {
                _nodes[0] = (element, priority);
                elementNodeIndices[element] = 0;
            }
        } else {
            if (_priorityComparer.Compare(priority, rootPriority) > 0) {
                MoveDownCustomComparer((element, priority), 0);
            } else {
                _nodes[0] = (element, priority);
                elementNodeIndices[element] = 0;
            }
        }
        _version++;
        return rootElement;
    }

    /// <summary>
    ///  Removes the minimal element from the <see cref="UniquePriorityQueue{TElement, TPriority}"/>,
    ///  and copies it to the <paramref name="element"/> parameter,
    ///  and its associated priority to the <paramref name="priority"/> parameter.
    /// </summary>
    /// <param name="element">The removed element.</param>
    /// <param name="priority">The priority associated with the removed element.</param>
    /// <returns>
    ///  <see langword="true"/> if the element is successfully removed;
    ///  <see langword="false"/> if the <see cref="UniquePriorityQueue{TElement, TPriority}"/> is empty.
    /// </returns>
    public bool TryDequeue([MaybeNullWhen(false)] out TElement element, [MaybeNullWhen(false)] out TPriority priority) {
        if (_size != 0) {
            (element, priority) = _nodes[0];
            RemoveRootNode();
            return true;
        }
        element = default;
        priority = default;
        return false;
    }

    /// <summary>
    ///  Returns a value that indicates whether there is a minimal element in the <see cref="UniquePriorityQueue{TElement, TPriority}"/>,
    ///  and if one is present, copies it to the <paramref name="element"/> parameter,
    ///  and its associated priority to the <paramref name="priority"/> parameter.
    ///  The element is not removed from the <see cref="UniquePriorityQueue{TElement, TPriority}"/>.
    /// </summary>
    /// <param name="element">The minimal element in the queue.</param>
    /// <param name="priority">The priority associated with the minimal element.</param>
    /// <returns>
    ///  <see langword="true"/> if there is a minimal element;
    ///  <see langword="false"/> if the <see cref="UniquePriorityQueue{TElement, TPriority}"/> is empty.
    /// </returns>
    public bool TryPeek([MaybeNullWhen(false)] out TElement element, [MaybeNullWhen(false)] out TPriority priority) {
        if (_size != 0) {
            (element, priority) = _nodes[0];
            return true;
        }
        element = default;
        priority = default;
        return false;
    }

    /// <summary>
    ///  Adds the specified element with associated priority to the <see cref="UniquePriorityQueue{TElement, TPriority}"/>,
    ///  and immediately removes the minimal element, returning the result.
    /// </summary>
    /// <param name="element">The element to add to the <see cref="UniquePriorityQueue{TElement, TPriority}"/>.</param>
    /// <param name="priority">The priority with which to associate the new element.</param>
    /// <returns>The minimal element removed after the enqueue operation.</returns>
    /// <remarks>
    ///  Implements an insert-then-extract heap operation that is generally more efficient
    ///  than sequencing Enqueue and Dequeue operations: in the worst case scenario only one
    ///  shift-down operation is required.
    /// </remarks>
    public TElement EnqueueDequeue(TElement element, TPriority priority) {
        if (_size != 0) {
            if (_elementMap.ContainsKey(element)) {
                throw new ArgumentException("An element with the same value already exists in the queue.", nameof(element));
            }
            var (rootElement, rootPriority) = _nodes[0];
            if (_priorityComparer == null) {
                if (Comparer<TPriority>.Default.Compare(priority, rootPriority) > 0) {
                    MoveDownDefaultComparer((element, priority), 0);
                    _version++;
                    return rootElement;
                }
            } else {
                if (_priorityComparer.Compare(priority, rootPriority) > 0) {
                    MoveDownCustomComparer((element, priority), 0);
                    _version++;
                    return rootElement;
                }
            }
        }
        return element;
    }

    /// <summary>
    ///  Enqueues a sequence of element/priority pairs to the <see cref="UniquePriorityQueue{TElement, TPriority}"/>.
    /// </summary>
    /// <param name="items">The pairs of elements and priorities to add to the queue.</param>
    /// <exception cref="ArgumentNullException">
    ///  The specified <paramref name="items"/> argument was <see langword="null"/>.
    /// </exception>
    public void EnqueueRange(IEnumerable<(TElement Element, TPriority Priority)> items) {
        ArgumentNullException.ThrowIfNull(items);

        int count = 0;
        var collection = items as ICollection<(TElement Element, TPriority Priority)>;
        if (collection is not null && (count = collection.Count) > _nodes.Length - _size) {
            Grow(checked(_size + count));
        }
        if (_size == 0) {
            // build using Heapify() if the queue is empty.
            if (collection is not null) {
                collection.CopyTo(_nodes, 0);
                _size = count;
            } else {
                int i = 0;
                (TElement, TPriority)[] nodes = _nodes;
                var elementNodeIndices = _elementMap;
                foreach ((TElement element, TPriority priority) in items) {
                    if (nodes.Length == i) {
                        Grow(i + 1);
                        nodes = _nodes;
                    }
                    elementNodeIndices[element] = i;
                    nodes[i++] = (element, priority);
                }
                _size = i;
            }
            _version++;
            if (_size > 1) {
                Heapify();
            }
        } else {
            foreach ((TElement element, TPriority priority) in items) {
                Enqueue(element, priority);
            }
        }
    }

    /// <summary>
    ///  Enqueues a sequence of elements pairs to the <see cref="UniquePriorityQueue{TElement, TPriority}"/>,
    ///  all associated with the specified priority.
    /// </summary>
    /// <param name="elements">The elements to add to the queue.</param>
    /// <param name="priority">The priority to associate with the new elements.</param>
    /// <exception cref="ArgumentNullException">
    ///  The specified <paramref name="elements"/> argument was <see langword="null"/>.
    /// </exception>
    public void EnqueueRange(IEnumerable<TElement> elements, TPriority priority) {
        ArgumentNullException.ThrowIfNull(elements);

        int count;
        if (elements is ICollection<TElement> collection &&
            (count = collection.Count) > _nodes.Length - _size) {
            Grow(checked(_size + count));
        }
        if (_size == 0) {
            // If the queue is empty just append the elements since they all have the same priority.
            int i = 0;
            (TElement, TPriority)[] nodes = _nodes;
            var elementNodeIndices = _elementMap;
            foreach (TElement element in elements) {
                if (nodes.Length == i) {
                    Grow(i + 1);
                    nodes = _nodes;
                }
                elementNodeIndices[element] = i;
                nodes[i++] = (element, priority);
            }
            _size = i;
            _version++;
        } else {
            foreach (TElement element in elements) {
                Enqueue(element, priority);
            }
        }
    }

    public bool Contains(TElement element)
        => _elementMap.ContainsKey(element);

    public TPriority GetElementPriority(TElement element) {
        if (!TryGetElementPriority(element, out var priority)) {
            throw new ArgumentException($"Element '{element}' is not in the queue.", nameof(element));
        }
        return priority;
    }

    public bool TryGetElementPriority(TElement element, [MaybeNullWhen(false)] out TPriority priority) {
        if (_elementMap.TryGetValue(element, out var index)) {
            priority = _nodes[index].Priority;
            return true;
        }
        priority = default;
        return false;
    }

    public void DecreasePriority(TElement element, TPriority newPriority) {
        if (!_elementMap.TryGetValue(element, out var index)) {
            throw new ArgumentException($"Element '{element}' is not in the queue.", nameof(element));
        }
        var nodePriority = _nodes[index].Priority;
        if (_priorityComparer == null) {
            if (Comparer<TPriority>.Default.Compare(newPriority, nodePriority) > 0) {
                throw new ArgumentException("New priority is greater than the current priority of the element.", nameof(newPriority));
            }
            MoveUpDefaultComparer((element, newPriority), index);
            _version++;
        } else {
            if (_priorityComparer.Compare(newPriority, nodePriority) > 0) {
                throw new ArgumentException("New priority is greater than the current priority of the element.", nameof(newPriority));
            }
            MoveUpCustomComparer((element, newPriority), index);
            _version++;
        }
    }

    /// <summary>
    /// Removes the first occurrence that equals the specified parameter.
    /// </summary>
    /// <param name="element">The element to try to remove.</param>
    /// <param name="removedElement">The actual element that got removed from the queue.</param>
    /// <param name="priority">The priority value associated with the removed element.</param>
    /// <param name="equalityComparer">The equality comparer governing element equality.</param>
    /// <returns><see langword="true"/> if matching entry was found and removed, <see langword="false"/> otherwise.</returns>
    /// <remarks>
    /// The method performs a linear-time scan of every element in the heap, removing the first value found to match the <paramref name="element"/> parameter.
    /// In case of duplicate entries, what entry does get removed is non-deterministic and does not take priority into account.
    ///
    /// If no <paramref name="equalityComparer"/> is specified, <see cref="EqualityComparer{TElement}.Default"/> will be used instead.
    /// </remarks>
    public bool Remove(TElement element, [MaybeNullWhen(false)] out TElement removedElement, [MaybeNullWhen(false)] out TPriority priority) {
        var elementNodeIndices = _elementMap;
        if (!elementNodeIndices.TryGetValue(element, out var index)) {
            removedElement = default;
            priority = default;
            return false;
        }
        elementNodeIndices.Remove(element);

        (TElement Element, TPriority Priority)[] nodes = _nodes;
        (removedElement, priority) = nodes[index];
        int newSize = --_size;

        if (index < newSize) {
            // We're removing an element from the middle of the heap.
            // Pop the last element in the collection and sift from the removed index.
            (TElement Element, TPriority Priority) lastNode = nodes[newSize];

            if (_priorityComparer == null) {
                if (Comparer<TPriority>.Default.Compare(lastNode.Priority, priority) < 0) {
                    MoveUpDefaultComparer(lastNode, index);
                } else {
                    MoveDownDefaultComparer(lastNode, index);
                }
            } else {
                if (_priorityComparer.Compare(lastNode.Priority, priority) < 0) {
                    MoveUpCustomComparer(lastNode, index);
                } else {
                    MoveDownCustomComparer(lastNode, index);
                }
            }
        }
        nodes[newSize] = default;
        _version++;
        return true;
    }

    /// <summary>
    ///  Removes all items from the <see cref="UniquePriorityQueue{TElement, TPriority}"/>.
    /// </summary>
    public void Clear() {
        if (RuntimeHelpers.IsReferenceOrContainsReferences<(TElement, TPriority)>()) {
            // Clear the elements so that the gc can reclaim the references
            Array.Clear(_nodes, 0, _size);
            _elementMap.Clear();
        }
        _size = 0;
        _version++;
    }

    /// <summary>
    ///  Ensures that the <see cref="UniquePriorityQueue{TElement, TPriority}"/> can hold up to
    ///  <paramref name="capacity"/> items without further expansion of its backing storage.
    /// </summary>
    /// <param name="capacity">The minimum capacity to be used.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    ///  The specified <paramref name="capacity"/> is negative.
    /// </exception>
    /// <returns>The current capacity of the <see cref="UniquePriorityQueue{TElement, TPriority}"/>.</returns>
    public int EnsureCapacity(int capacity) {
        ArgumentOutOfRangeException.ThrowIfNegative(capacity);

        if (_nodes.Length < capacity) {
            Grow(capacity);
            _version++;
        }
        return _nodes.Length;
    }

    /// <summary>
    ///  Sets the capacity to the actual number of items in the <see cref="UniquePriorityQueue{TElement, TPriority}"/>,
    ///  if that is less than 90 percent of current capacity.
    /// </summary>
    /// <remarks>
    ///  This method can be used to minimize a collection's memory overhead
    ///  if no new elements will be added to the collection.
    /// </remarks>
    public void TrimExcess() {
        int threshold = (int)(_nodes.Length * 0.9);
        if (_size < threshold) {
            Array.Resize(ref _nodes, _size);
            _version++;
        }
    }

    /// <summary>
    /// Grows the priority queue to match the specified min capacity.
    /// </summary>
    private void Grow(int minCapacity) {
        Debug.Assert(_nodes.Length < minCapacity);

        const int GrowFactor = 2;
        const int MinimumGrow = 4;

        int newcapacity = GrowFactor * _nodes.Length;

        // Allow the queue to grow to maximum possible capacity (~2G elements) before encountering overflow.
        // Note that this check works even when _nodes.Length overflowed thanks to the (uint) cast
        if ((uint)newcapacity > Array.MaxLength) {
            newcapacity = Array.MaxLength;
        }
        // Ensure minimum growth is respected.
        newcapacity = Math.Max(newcapacity, _nodes.Length + MinimumGrow);

        // If the computed capacity is still less than specified, set to the original argument.
        // Capacities exceeding Array.MaxLength will be surfaced as OutOfMemoryException by Array.Resize.
        if (newcapacity < minCapacity) {
            newcapacity = minCapacity;
        }
        Array.Resize(ref _nodes, newcapacity);
        _elementMap.EnsureCapacity(newcapacity);
    }

    /// <summary>
    /// Removes the node from the root of the heap
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RemoveRootNode() {
        int lastNodeIndex = --_size;
        _version++;

        _elementMap.Remove(_nodes[0].Element);
        if (lastNodeIndex > 0) {
            var lastNode = _nodes[lastNodeIndex];
            if (_priorityComparer == null) {
                MoveDownDefaultComparer(lastNode, 0);
            } else {
                MoveDownCustomComparer(lastNode, 0);
            }
        }
        if (RuntimeHelpers.IsReferenceOrContainsReferences<(TElement, TPriority)>()) {
            _nodes[lastNodeIndex] = default;
        }
    }

    /// <summary>
    /// Gets the index of an element's parent.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetParentIndex(int index) => (index - 1) >> Log2Arity;

    /// <summary>
    /// Gets the index of the first child of an element.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetFirstChildIndex(int index) => (index << Log2Arity) + 1;

    /// <summary>
    /// Converts an unordered list into a heap.
    /// </summary>
    private void Heapify() {
        // Leaves of the tree are in fact 1-element heaps, for which there
        // is no need to correct them. The heap property needs to be restored
        // only for higher nodes, starting from the first node that has children.
        // It is the parent of the very last element in the array.
        (TElement Element, TPriority Priority)[] nodes = _nodes;
        int lastParentWithChildren = GetParentIndex(_size - 1);

        if (_priorityComparer == null) {
            for (int index = lastParentWithChildren; index >= 0; --index) {
                MoveDownDefaultComparer(nodes[index], index);
            }
        } else {
            for (int index = lastParentWithChildren; index >= 0; --index) {
                MoveDownCustomComparer(nodes[index], index);
            }
        }
    }

    /// <summary>
    /// Moves a node up in the tree to restore heap order.
    /// </summary>
    private void MoveUpDefaultComparer((TElement Element, TPriority Priority) node, int nodeIndex) {
        // Instead of swapping items all the way to the root, we will perform a similar optimization as in the insertion sort.
        Debug.Assert(_priorityComparer is null);
        Debug.Assert(0 <= nodeIndex && nodeIndex < _size);

        (TElement Element, TPriority Priority)[] nodes = _nodes;
        var elementNodeIndices = _elementMap;

        while (nodeIndex > 0) {
            int parentIndex = GetParentIndex(nodeIndex);
            (TElement Element, TPriority Priority) parent = nodes[parentIndex];

            if (Comparer<TPriority>.Default.Compare(node.Priority, parent.Priority) < 0) {
                nodes[nodeIndex] = parent;
                elementNodeIndices[parent.Element] = nodeIndex;
                nodeIndex = parentIndex;
            } else {
                break;
            }
        }

        nodes[nodeIndex] = node;
        elementNodeIndices[node.Element] = nodeIndex;
    }

    /// <summary>
    /// Moves a node up in the tree to restore heap order.
    /// </summary>
    private void MoveUpCustomComparer((TElement Element, TPriority Priority) node, int nodeIndex) {
        // Instead of swapping items all the way to the root, we will perform
        // a similar optimization as in the insertion sort.
        Debug.Assert(_priorityComparer is not null);
        Debug.Assert(0 <= nodeIndex && nodeIndex < _size);

        IComparer<TPriority> comparer = _priorityComparer;
        (TElement Element, TPriority Priority)[] nodes = _nodes;
        var elementNodeIndices = _elementMap;

        while (nodeIndex > 0) {
            int parentIndex = GetParentIndex(nodeIndex);
            (TElement Element, TPriority Priority) parent = nodes[parentIndex];

            if (comparer.Compare(node.Priority, parent.Priority) < 0) {
                nodes[nodeIndex] = parent;
                elementNodeIndices[parent.Element] = nodeIndex;
                nodeIndex = parentIndex;
            } else {
                break;
            }
        }
        nodes[nodeIndex] = node;
        elementNodeIndices[node.Element] = nodeIndex;
    }

    /// <summary>
    /// Moves a node down in the tree to restore heap order.
    /// </summary>
    private void MoveDownDefaultComparer((TElement Element, TPriority Priority) node, int nodeIndex) {
        // The node to move down will not actually be swapped every time.
        // Rather, values on the affected path will be moved up, thus leaving a free spot
        // for this value to drop in. Similar optimization as in the insertion sort.
        Debug.Assert(_priorityComparer is null);
        Debug.Assert(0 <= nodeIndex && nodeIndex < _size);

        (TElement Element, TPriority Priority)[] nodes = _nodes;
        var elementNodeIndices = _elementMap;
        int size = _size;

        int i;
        while ((i = GetFirstChildIndex(nodeIndex)) < size) {
            // Find the child node with the minimal priority
            (TElement Element, TPriority Priority) minChild = nodes[i];
            int minChildIndex = i;

            int childIndexUpperBound = Math.Min(i + Arity, size);
            while (++i < childIndexUpperBound) {
                (TElement Element, TPriority Priority) nextChild = nodes[i];
                if (Comparer<TPriority>.Default.Compare(nextChild.Priority, minChild.Priority) < 0) {
                    minChild = nextChild;
                    minChildIndex = i;
                }
            }
            // Heap property is satisfied; insert node in this location.
            if (Comparer<TPriority>.Default.Compare(node.Priority, minChild.Priority) <= 0) {
                break;
            }
            // Move the minimal child up by one node and continue recursively from its location.
            nodes[nodeIndex] = minChild;
            elementNodeIndices[minChild.Element] = nodeIndex;
            nodeIndex = minChildIndex;
        }
        nodes[nodeIndex] = node;
        elementNodeIndices[node.Element] = nodeIndex;
    }

    /// <summary>
    /// Moves a node down in the tree to restore heap order.
    /// </summary>
    private void MoveDownCustomComparer((TElement Element, TPriority Priority) node, int nodeIndex) {
        // The node to move down will not actually be swapped every time.
        // Rather, values on the affected path will be moved up, thus leaving a free spot
        // for this value to drop in. Similar optimization as in the insertion sort.
        Debug.Assert(_priorityComparer is not null);
        Debug.Assert(0 <= nodeIndex && nodeIndex < _size);

        IComparer<TPriority> comparer = _priorityComparer;
        (TElement Element, TPriority Priority)[] nodes = _nodes;
        var elementNodeIndices = _elementMap;
        int size = _size;

        int i;
        while ((i = GetFirstChildIndex(nodeIndex)) < size) {
            // Find the child node with the minimal priority
            (TElement Element, TPriority Priority) minChild = nodes[i];
            int minChildIndex = i;

            int childIndexUpperBound = Math.Min(i + Arity, size);
            while (++i < childIndexUpperBound) {
                (TElement Element, TPriority Priority) nextChild = nodes[i];
                if (comparer.Compare(nextChild.Priority, minChild.Priority) < 0) {
                    minChild = nextChild;
                    minChildIndex = i;
                }
            }
            // Heap property is satisfied; insert node in this location.
            if (comparer.Compare(node.Priority, minChild.Priority) <= 0) {
                break;
            }
            // Move the minimal child up by one node and continue recursively from its location.
            nodes[nodeIndex] = minChild;
            elementNodeIndices[minChild.Element] = nodeIndex;
            nodeIndex = minChildIndex;
        }
        nodes[nodeIndex] = node;
        elementNodeIndices[node.Element] = nodeIndex;
    }

    /// <summary>
    /// Initializes the custom comparer to be used internally by the heap.
    /// </summary>
    private static IComparer<TPriority>? InitializeComparer(IComparer<TPriority>? comparer) {
        if (typeof(TPriority).IsValueType) {
            if (comparer == Comparer<TPriority>.Default) {
                // if the user manually specifies the default comparer, revert to using the optimized path.
                return null;
            }
            return comparer;
        } else {
            // Currently the JIT doesn't optimize direct Comparer<T>.Default.Compare
            // calls for reference types, so we want to cache the comparer instance instead.
            // TODO https://github.com/dotnet/runtime/issues/10050: Update if this changes in the future.
            return comparer ?? Comparer<TPriority>.Default;
        }
    }

    /// <summary>
    /// Initializes the custom comparer to be used internally by the heap.
    /// </summary>
    private static IEqualityComparer<TElement>? InitializeEquater(IEqualityComparer<TElement>? equater) {
        if (typeof(TElement).IsValueType) {
            if (equater == EqualityComparer<TElement>.Default) {
                // if the user manually specifies the default comparer, revert to using the optimized path.
                return null;
            }
            return equater;
        } else {
            // Currently the JIT doesn't optimize direct Comparer<T>.Default.Compare
            // calls for reference types, so we want to cache the comparer instance instead.
            // TODO https://github.com/dotnet/runtime/issues/10050: Update if this changes in the future.
            return equater ?? EqualityComparer<TElement>.Default;
        }
    }

    /// <summary>
    ///  Enumerates the contents of a <see cref="UniquePriorityQueue{TElement, TPriority}"/>, without any ordering guarantees.
    /// </summary>
    [DebuggerDisplay("Count = {Count}")]
    public sealed class UnorderedItemsCollection : IReadOnlyCollection<(TElement Element, TPriority Priority)>, ICollection {
        internal readonly UniquePriorityQueue<TElement, TPriority> _queue;

        internal UnorderedItemsCollection(UniquePriorityQueue<TElement, TPriority> queue) => _queue = queue;

        public int Count => _queue._size;
        object ICollection.SyncRoot => this;
        bool ICollection.IsSynchronized => false;

        void ICollection.CopyTo(Array array, int index) {
            ArgumentNullException.ThrowIfNull(array);

            if (array.Rank != 1) {
                throw new ArgumentException("Only single dimensional arrays are supported for the requested action.", nameof(array));
            }
            if (array.GetLowerBound(0) != 0) {
                throw new ArgumentException("The lower bound of target array must be zero.", nameof(array));
            }
            if (index < 0 || index > array.Length) {
                throw new ArgumentOutOfRangeException(nameof(index), index, "Index was out of range. Must be non-negative and less than or equal to the size of the collection.");
            }
            if (array.Length - index < _queue._size) {
                throw new ArgumentException("Offset and length were out of bounds for the array or count is greater than the number of elements from index to the end of the source collection.");
            }
            try {
                Array.Copy(_queue._nodes, 0, array, index, _queue._size);
            } catch (ArrayTypeMismatchException) {
                throw new ArgumentException("Target array type is not compatible with the type of items in the collection.", nameof(array));
            }
        }

        /// <summary>
        ///  Enumerates the element and priority pairs of a <see cref="UniquePriorityQueue{TElement, TPriority}"/>,
        ///  without any ordering guarantees.
        /// </summary>
        public struct Enumerator : IEnumerator<(TElement Element, TPriority Priority)> {
            private readonly UniquePriorityQueue<TElement, TPriority> _queue;
            private readonly int _version;
            private int _index;
            private (TElement, TPriority) _current;

            internal Enumerator(UniquePriorityQueue<TElement, TPriority> queue) {
                _queue = queue;
                _index = 0;
                _version = queue._version;
                _current = default;
            }

            /// <summary>
            /// Releases all resources used by the <see cref="Enumerator"/>.
            /// </summary>
            public void Dispose() { }

            /// <summary>
            /// Advances the enumerator to the next element of the <see cref="UnorderedItems"/>.
            /// </summary>
            /// <returns><see langword="true"/> if the enumerator was successfully advanced to the next element; <see langword="false"/> if the enumerator has passed the end of the collection.</returns>
            public bool MoveNext() {
                UniquePriorityQueue<TElement, TPriority> localQueue = _queue;

                if (_version != localQueue._version) {
                    throw new InvalidOperationException("Collection was modified after the enumerator was instantiated.");
                }
                if ((uint)_index < (uint)localQueue._size) {
                    _current = localQueue._nodes[_index];
                    _index++;
                    return true;
                }
                _current = default;
                _index = -1;
                return false;
            }

            /// <summary>
            /// Gets the element at the current position of the enumerator.
            /// </summary>
            public (TElement Element, TPriority Priority) Current => _current;
            object IEnumerator.Current => _current;

            void IEnumerator.Reset() {
                if (_version != _queue._version) {
                    throw new InvalidOperationException("Collection was modified after the enumerator was instantiated.");
                }
                _index = 0;
                _current = default;
            }
        }

        /// <summary>
        /// Returns an enumerator that iterates through the <see cref="UnorderedItems"/>.
        /// </summary>
        /// <returns>An <see cref="Enumerator"/> for the <see cref="UnorderedItems"/>.</returns>
        public Enumerator GetEnumerator() => new(_queue);

        IEnumerator<(TElement Element, TPriority Priority)> IEnumerable<(TElement Element, TPriority Priority)>.GetEnumerator() =>
            _queue.Count == 0 ? EnumerableHelpers.GetEmptyEnumerator<(TElement Element, TPriority Priority)>() :
            GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}

internal static class EnumerableHelpers {
    /// <summary>Calls Reset on an enumerator instance.</summary>
    /// <remarks>Enables Reset to be called without boxing on a struct enumerator that lacks a public Reset.</remarks>
    internal static void Reset<T>(ref T enumerator) where T : IEnumerator => enumerator.Reset();

    /// <summary>Gets an enumerator singleton for an empty collection.</summary>
    internal static IEnumerator<T> GetEmptyEnumerator<T>() =>
        ((IEnumerable<T>)[]).GetEnumerator();

    /// <summary>Converts an enumerable to an array using the same logic as List{T}.</summary>
    /// <param name="source">The enumerable to convert.</param>
    /// <param name="length">The number of items stored in the resulting array, 0-indexed.</param>
    /// <returns>
    /// The resulting array.  The length of the array may be greater than <paramref name="length"/>,
    /// which is the actual number of elements in the array.
    /// </returns>
    internal static T[] ToArray<T>(IEnumerable<T> source, out int length) {
        // Copied from Array.MaxLength in System.Private.CoreLib/src/libraries/System.Private.CoreLib/src/System/Array.cs
        const int ArrayMaxLength = 0X7FFFFFC7;
#if NET
        Debug.Assert(Array.MaxLength == ArrayMaxLength);
#endif
        if (source is ICollection<T> ic) {
            int count = ic.Count;
            if (count != 0) {
                // Allocate an array of the desired size, then copy the elements into it. Note that this has the same
                // issue regarding concurrency as other existing collections like List<T>. If the collection size
                // concurrently changes between the array allocation and the CopyTo, we could end up either getting an
                // exception from overrunning the array (if the size went up) or we could end up not filling as many
                // items as 'count' suggests (if the size went down).  This is only an issue for concurrent collections
                // that implement ICollection<T>, which as of .NET 4.6 is just ConcurrentDictionary<TKey, TValue>.
                T[] arr = new T[count];
                ic.CopyTo(arr, 0);
                length = count;
                return arr;
            }
        } else {
            using (var en = source.GetEnumerator()) {
                if (en.MoveNext()) {
                    const int DefaultCapacity = 4;
                    T[] arr = new T[DefaultCapacity];
                    arr[0] = en.Current;
                    int count = 1;

                    while (en.MoveNext()) {
                        if (count == arr.Length) {
                            // This is the same growth logic as in List<T>:
                            // If the array is currently empty, we make it a default size.  Otherwise, we attempt to
                            // double the size of the array.  Doubling will overflow once the size of the array reaches
                            // 2^30, since doubling to 2^31 is 1 larger than Int32.MaxValue.  In that case, we instead
                            // constrain the length to be Array.MaxLength (this overflow check works because of the
                            // cast to uint).
                            int newLength = count << 1;
                            if ((uint)newLength > ArrayMaxLength) {
                                newLength = ArrayMaxLength <= count ? count + 1 : ArrayMaxLength;
                            }

                            Array.Resize(ref arr, newLength);
                        }

                        arr[count++] = en.Current;
                    }
                    length = count;
                    return arr;
                }
            }
        }
        length = 0;
        return [];
    }
}
