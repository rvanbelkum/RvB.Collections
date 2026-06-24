using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace RvB.Collections;

/// <summary>
/// A Fibonacci heap implementation of the priority queue
/// </summary>
public class FibonacciPriorityQueue<TElement, TPriority> : IEnumerable<TElement> {
    /// <summary>
    /// Represents a single node in the fibonacci heap
    /// </summary>
    public sealed class Node {
        internal Node? Parent { get; set; }
        internal Node? Child { get; set; }
        internal Node Left { get; set; }
        internal Node Right { get; set; }
        internal int Degree { get; set; }
        internal bool IsMarked { get; set; }

        /// <summary>
        /// The actual value of the user-specified type
        /// </summary>
        public TElement Value { get; internal set; }

        /// <summary>
        /// The priority value
        /// </summary>
        public TPriority Priority { get; internal set; }

        internal Node(TElement value, TPriority priority) {
            Left = this;
            Right = this;
            Value = value;
            Priority = priority;
        }

        public override string ToString() => $"{Priority}: {Value}";
    }

    #region Private fields
    private int _count;
    private Node? _minNode;
    private readonly IComparer<TPriority> _comparer;


    /// <summary>
    /// Initializes a new instance of the <see cref="FibonacciPriorityQueue{TElement, TPriority}"/>
    /// </summary>
    public FibonacciPriorityQueue() : this(Comparer<TPriority>.Default) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="FibonacciPriorityQueue{TElement, TPriority}"/> with the specified priority comparer.
    /// </summary>
    /// <remarks>The provided comparer is used to determine the ordering of elements based on their priority.</remarks>
    /// <param name="comparer">An implementation of <see cref="IComparer{TPriority}"/> used to compare the priority values of elements in the queue.
    /// When using <see cref="ReverseComparer{TPriority}.Default"/>, the queue will effectively act as a max-heap
    /// </param>
    public FibonacciPriorityQueue(IComparer<TPriority> comparer) {
        _comparer = comparer ?? Comparer<TPriority>.Default;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FibonacciPriorityQueue{TElement, TPriority}"/> class that contains elements copied from the specified collection
    /// </summary>
    /// <param name="collection">The collection whose elements are copied to the <see cref="FibonacciPriorityQueue{TElement, TPriority}"/></param>
    public FibonacciPriorityQueue(IEnumerable<(TElement element, TPriority priority)> collection) : this(collection, Comparer<TPriority>.Default) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="FibonacciPriorityQueue{TElement, TPriority}"/> class that contains elements copied from the specified collection
    /// </summary>
    /// <param name="collection">The collection whose elements are copied to the <see cref="FibonacciPriorityQueue{TElement, TPriority}"/></param>
    /// <param name="comparer">An implementation of <see cref="IComparer{TPriority}"/> used to compare the priority values of elements in the queue.
    /// When using <see cref="ReverseComparer{TPriority}.Default"/>, the queue will effectively act as a max-heap
    /// </param>
    public FibonacciPriorityQueue(IEnumerable<(TElement element, TPriority priority)> collection, IComparer<TPriority> comparer) : this(comparer) {
        foreach (var (element, priority) in collection) {
            Enqueue(element, priority);
        }
    }
    #endregion

    #region Public properties
    /// <inheritdoc/>
    public int Count => _count;
    #endregion

    #region Public methods
    /// <summary>
    /// Clear all elements from the queue
    /// </summary>
    public void Clear() {
        _minNode = null;
        _count = 0;
    }

    /// <summary>
    /// Determines whether the queue contains the specified element
    /// </summary>
    /// <param name="element"></param>
    /// <exception cref="ArgumentNullException"></exception>
    /// <returns><see langword="true"/> if the queue contains the specified element, <see langword="false"/> otherwise</returns>
    public bool Contains(TElement element)
        => TryGetNode(element, out _);

    /// <summary>
    /// Attempts to retrieve the node associated with the specified element.
    /// </summary>
    /// <remarks>If the specified element is null, the method returns false and sets the output parameter to
    /// null.</remarks>
    /// <param name="element">The element for which to find the corresponding node. Must not be null.</param>
    /// <param name="node">When this method returns, contains the node associated with the specified element if found; otherwise, it is
    /// null.</param>
    /// <returns>true if the node was found; otherwise, false.</returns>
    public bool TryGetNode(TElement element, [MaybeNullWhen(false)] out Node node) {
        if (element == null) {
            node = default;
            return false;
        }
        node = Find(element, _minNode);
        return node != null;
    }

    /// <summary>
    /// Sets a new priority for the specified node. The new value has to be lower as defined by the <see cref="IComparer{TPriority}"/> comparer, otherwise an <see cref="ArgumentException"/> is thrown.
    /// </summary>
    /// <param name="node"><see cref="Node"/> to be modified</param>
    /// <param name="newPriority">The new priority</param>
    public void AdjustPriority(Node node, TPriority newPriority) {
        if (_comparer.Compare(newPriority, node.Priority) > 0) {
            throw new ArgumentException("New priority exceeds old", nameof(newPriority));
        }
        AdjustPriorityUnchecked(node, newPriority);
    }

    /// <summary>
    /// Returns the element with the highest priority value and removes it from the queue
    /// </summary>
    /// <exception cref="InvalidOperationException"></exception>
    /// <returns>The element with the highest priority</returns>
    public TElement Dequeue() {
        if (TryDequeue(out var element, out _))
            return element;
        throw new InvalidOperationException("Queue is empty");
    }

    /// <summary>
    /// Attempts to remove and return the element at the beginning of the queue.
    /// </summary>
    /// <remarks>This method enables safe removal of an element from the queue without throwing an exception if the queue is empty.</remarks>
    /// <param name="element">When this method returns <see langword="true"/>, contains the element removed from the queue; otherwise,
    /// contains the default value for the type <typeparamref name="TElement"/>.</param>
    /// <returns><see langword="true"/> if an element was successfully removed and returned; otherwise, <see langword="false"/>.</returns>
    public bool TryDequeue([MaybeNullWhen(false)] out TElement element)
        => TryDequeue(out element, out _);

    /// <summary>
    /// Attempts to remove and return the element with the highest priority from the priority queue.
    /// </summary>
    /// <remarks>This method enables safe removal of an element from the queue without throwing an exception if the queue is empty.</remarks>
    /// <param name="element">When this method returns <see langword="true"/>, contains the value of the element removed from the queue;
    /// otherwise, set to the default value for the type <typeparamref name="TElement"/>.</param>
    /// <param name="priority">When this method returns <see langword="true"/>, contains the priority of the element removed from the queue;
    /// otherwise, set to the default value for the type <typeparamref name="TPriority"/>.</param>
    /// <returns><see langword="true"/> if an element was successfully removed from the queue; otherwise, <see
    /// langword="false"/>.</returns>
    public bool TryDequeue([MaybeNullWhen(false)] out TElement element, [MaybeNullWhen(false)] out TPriority priority) {
        if (IsEmpty()) {
            element = default;
            priority = default;
            return false;
        }
        _count--;

        var minNode = _minNode;
        var minElem = minNode;

        if (minElem.Right == minElem) {
            minNode = null;
        } else {
            minElem.Left.Right = minElem.Right;
            minElem.Right.Left = minElem.Left;
            minNode = minElem.Right;
        }

        if (minElem.Child != null) {
            var node = minElem.Child;
            do {
                node.Parent = null;
                node = node.Right;
            }
            while (node != minElem.Child);
        }

        if (minElem.Child != null) {
            minNode = MergeNodes(minNode, minElem.Child);
        } else if (minNode == null) {
            _minNode = minNode;
            element = minElem.Value;
            priority = minElem.Priority;
            return true;
        }

        List<Node?> treeTable = new(_count);
        List<Node> toVisit = new(_count);

        Node curr = minNode;
        do {
            toVisit.Add(curr);
            curr = curr.Right;
        } while (curr != minNode);

        foreach (var item in toVisit) {
            curr = item;
            while (true) {
                while (curr.Degree >= treeTable.Count) {
                    treeTable.Add(null);
                }

                if (treeTable[curr.Degree] == null) {
                    treeTable[curr.Degree] = curr;
                    break;
                }

                var other = treeTable[curr.Degree];
                treeTable[curr.Degree] = null;

                (Node min, Node max) = _comparer.Compare(other!.Priority, curr.Priority) < 0 ? (other, curr) : (curr, other);

                max.Right.Left = max.Left;
                max.Left.Right = max.Right;

                max.Right = max.Right = max;
                min.Child = MergeNodes(min.Child, max);

                max.Parent = min;

                max.IsMarked = false;

                min.Degree++;

                curr = min;
            }

            if (_comparer.Compare(curr.Priority, minNode.Priority) <= 0) {
                minNode = curr;
            }
        }
        _minNode = minNode;
        element = minElem.Value;
        priority = minElem.Priority;
        return true;
    }

    /// <summary>
    /// Inserts an element to the queue with a specified priority value
    /// </summary>
    /// /// <exception cref="ArgumentNullException"></exception>
    /// <param name="element">The element to be added to the queue</param>
    /// <param name="priority">The priority value</param>
    public Node Enqueue(TElement element, TPriority priority) {
        ArgumentNullException.ThrowIfNull(element);

        var newNode = new Node(element, priority);
        _minNode = MergeNodes(_minNode, newNode);
        _count++;
        return newNode;
    }

    /// <summary>
    /// Determines if the queue contains no elements
    /// </summary>
    /// <returns><see langword="true"/> if the queue contains no elements, <see langword="false"/> otherwise</returns>
    [MemberNotNullWhen(false, nameof(_minNode))]
    public bool IsEmpty() {
        return _minNode == null;
    }

    /// <summary>
    /// Returns the element with the highest priority value without removing it from the queue
    /// </summary>
    /// <exception cref="InvalidOperationException"></exception>
    /// <returns>The element with the highest priority</returns>
    public TElement Peek() {
        return IsEmpty() ? throw new InvalidOperationException("Queue is empty") : _minNode.Value;
    }

    /// <summary>
    /// Attempts to retrieve the element and its associated priority at the front of the queue without removing them.
    /// </summary>
    /// <remarks>This method does not modify the queue. Use it to inspect the front element and its priority
    /// without removing them.</remarks>
    /// <param name="element">When this method returns <see langword="true"/>, contains the element at the front of the queue; otherwise,
    /// contains the default value for the element type.</param>
    /// <param name="priority">When this method returns <see langword="true"/>, contains the priority associated with the element at the front
    /// of the queue; otherwise, contains the default value for the priority type.</param>
    /// <returns><see langword="true"/> if the operation succeeds and the element and priority are set; otherwise, <see
    /// langword="false"/> if the queue is empty.</returns>
    public bool TryPeek([MaybeNullWhen(false)] out TElement element, [MaybeNullWhen(false)] out TPriority priority) {
        if (IsEmpty()) {
            element = default;
            priority = default;
            return false;
        }
        element = _minNode.Value;
        priority = _minNode.Priority;
        return true;
    }

    /// <summary>
    /// Removes an element from the queue
    /// </summary>
    /// <param name="element">Element to remove from queue</param>
    /// <returns><see langword="true"/> if the element is successfuly removed, otherwise <see langword="false"/></returns>
    public bool Remove(TElement element) {
        var node = Find(element, _minNode);
        if (node == null) {
            return false;
        }
        Remove(node);
        return true;
    }

    /// <summary>
    /// Removes a node from the queue
    /// </summary>
    /// <param name="node"><see cref="Node"/> to be removed</param>
    public void Remove(Node node) {
        AdjustPriorityUnchecked(node);
        Dequeue();
    }
    #endregion

    #region Private methods
    private Node MergeNodes(Node? node1, Node node2) {
        if (node1 == null) {
            return node2;
        } else {
            //Node oneRight = one!.Right;
            //one.Right = two!.Right;
            //one.Right.Left = one;
            //two.Right = oneRight;
            //two.Right.Left = two;

            (node1.Right, node2.Right.Left, node2.Right, node1.Right.Left) = (node2.Right, node1, node1.Right, node2);
            return _comparer.Compare(node1.Priority, node2.Priority) < 0 ? node1 : node2;
        }
    }

    private void CutNode(Node node) {
        node.IsMarked = false;

        if (node.Parent == null) {
            return;
        }
        if (node.Right != node) {
            node.Right.Left = node.Left;
            node.Left.Right = node.Right;
        }
        if (node.Parent.Child == node) {
            if (node.Right != node) {
                node.Parent.Child = node.Right;
            } else {
                node.Parent.Child = null;
            }
        }
        node.Parent.Degree--;

        node.Left = node.Right = node;
        _minNode = MergeNodes(_minNode, node);

        if (node.Parent.IsMarked) {
            CutNode(node.Parent);
        } else {
            node.Parent.IsMarked = true;
        }

        node.Parent = null;
    }

    private void AdjustPriorityUnchecked(Node node, TPriority priority) {
        node.Priority = priority;

        if (node.Parent != null && _comparer.Compare(node.Priority, node.Parent.Priority) <= 0) {
            CutNode(node);
        }
        if (_comparer.Compare(node.Priority, _minNode!.Priority) <= 0) {
            _minNode = node;
        }
    }

    private void AdjustPriorityUnchecked(Node node) {
        if (node.Parent != null) {
            CutNode(node);
        }
        _minNode = node;
    }

    private static Node? Find(TElement element, Node? startNode) {
        if (startNode == null) {
            return null;
        }

        Node node = startNode;
        Node? found = null;
        do {
            if (node.Value != null && node.Value.Equals(element)) {
                return node;
            } else {
                var k = node.Child;
                if (k != null && found == null) {
                    found = Find(element, k);
                }
                node = node.Right;
            }
        }
        while (node != startNode && found == null);
        return null;
    }
    #endregion

    #region IEnumerable interface implementation
    /// <inheritdoc/>
    public IEnumerator<TElement> GetEnumerator() {
        if (IsEmpty()) {
            yield break;
        }
        foreach (var item in Enumerate()) {
            yield return item;
        }
    }

    private IEnumerable<TElement> Enumerate() {
        if (IsEmpty()) {
            yield break;
        }
        var current = _minNode;
        do {
            foreach (var node in EnumerateBranch(current)) {
                yield return node;
            }
            current = current.Right;
        }
        while (current != _minNode);
    }

    private static IEnumerable<TElement> EnumerateBranch(Node root) {
        if (root.Child != null) {
            var current = root.Child;
            do {
                foreach (var node in EnumerateBranch(current)) {
                    yield return node;
                }
                current = current.Right;
            }
            while (current != root.Child);
        }
        yield return root.Value;
    }

    IEnumerator IEnumerable.GetEnumerator() {
        return GetEnumerator();
    }
    #endregion
}
