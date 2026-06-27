namespace RvB.Collections;

/// <summary>
/// Provides extension methods and enumerator types for generating permutations of a sequence.
/// </summary>
/// <remarks>
/// - Use <see cref="EnumerateUniquePermutations{T}"/> to enumerate unique permutations in lexicographic order (requires a comparer).
/// - Use <see cref="EnumeratePermutations{T}"/> to enumerate all permutations using Heap's algorithm (may include duplicates if input contains duplicates).
/// Both enumerators operate in-place on an internal array and return a <see cref="ReadOnlySpan{T}"/> view into that array for each permutation.
/// The underlying array is mutated between iterations; copy the contents if you need to retain a snapshot.
/// </remarks>
public static class Permutations {
    /// <summary>
    /// Creates an enumerator that yields unique permutations of <paramref name="elements"/> in lexicographic order.
    /// </summary>
    /// <typeparam name="T">Type of the elements.</typeparam>
    /// <param name="elements">The source sequence to permute. The sequence is copied internally.</param>
    /// <param name="comparer">
    /// Optional comparer used to determine lexicographic order. If <see langword="null"/>, <see cref="Comparer{T}.Default"/> is used.
    /// </param>
    /// <returns>
    /// A <see cref="Pandita{T}"/> enumerator that implements the foreach pattern and yields each unique permutation as a <see cref="ReadOnlySpan{T}"/>.
    /// </returns>
    public static Pandita<T> EnumerateUniquePermutations<T>(this IEnumerable<T> elements, IComparer<T>? comparer = null)
        => new(elements, comparer);

    /// <summary>
    /// Creates an enumerator that yields all permutations of <paramref name="elements"/> using Heap's algorithm.
    /// </summary>
    /// <typeparam name="T">Type of the elements.</typeparam>
    /// <param name="elements">The source sequence to permute. The sequence is copied internally.</param>
    /// <returns>
    /// A <see cref="Heaps{T}"/> enumerator that implements the foreach pattern and yields each permutation as a <see cref="ReadOnlySpan{T}"/>.
    /// </returns>
    public static Heaps<T> EnumeratePermutations<T>(this IEnumerable<T> elements)
        => new(elements);

    /// <summary>
    /// Enumerator that generates unique permutations in lexicographic order.
    /// </summary>
    /// <remarks>
    /// - The implementation follows the classic "next permutation" algorithm (sort first, then repeatedly compute the next lexicographic permutation).
    /// - The enumerator is a <see langword="struct"/> and follows the foreach pattern via <see cref="GetEnumerator"/>.
    /// - <see cref="Current"/> returns a <see cref="ReadOnlySpan{T}"/> over an internal array that is mutated in-place on each iteration.
    /// - Not thread-safe.
    /// </remarks>
    public struct Pandita<T> {
        private readonly T[] _elements;
        private readonly IComparer<T> _comparer;
        private bool _firstEnumerator = true;
        private bool _firstIteration = true;

        /// <summary>
        /// Initializes a new <see cref="Pandita{T}"/> enumerator.
        /// </summary>
        /// <param name="elements">Sequence to enumerate permutations of. The sequence is copied into an internal array.</param>
        /// <param name="comparer">Comparer used for ordering; if <see langword="null"/>, <see cref="Comparer{T}.Default"/> is used.</param>
        public Pandita(IEnumerable<T> elements, IComparer<T>? comparer = null) {
            _elements = [.. elements];
            _comparer = comparer ?? Comparer<T>.Default;
        }

        /// <summary>
        /// Returns an enumerator instance suitable for use in a foreach loop.
        /// </summary>
        /// <remarks>
        /// Because this type is a struct it returns itself on the first call and returns a fresh enumerator for subsequent calls,
        /// preserving expected foreach semantics for multiple enumerations.
        /// </remarks>
        public Pandita<T> GetEnumerator() {
            if (_firstEnumerator) {
                _firstEnumerator = false;
                return this;
            }
            return new(_elements, _comparer) { _firstEnumerator = false };
        }

        /// <summary>
        /// Gets the current permutation as a <see cref="ReadOnlySpan{T}"/> over the internal array.
        /// </summary>
        /// <remarks>
        /// The returned span is a view into the enumerator's internal buffer which is mutated by subsequent iterations.
        /// If you need a stable copy, call <c>span.ToArray()</c>.
        /// </remarks>
        public readonly ReadOnlySpan<T> Current => _elements.AsSpan();

        /// <summary>
        /// Advances the enumerator to the next unique permutation.
        /// </summary>
        /// <returns><see langword="true"/> if the next permutation is available; otherwise <see langword="false"/>.</returns>
        /// <remarks>
        /// On the first call this method sorts the internal array according to the provided comparer and yields the first (lowest) permutation.
        /// Subsequent calls compute the next lexicographic permutation. When no further permutations exist, <see langword="false"/> is returned.
        /// </remarks>
        public bool MoveNext() {
            if (_firstIteration) {
                _firstIteration = false;
                Array.Sort(_elements, _comparer);
                return true;
            }
            int i = _elements.Length - 2;
            var comparer = _comparer;
            while (i >= 0 && comparer.Compare(_elements[i], _elements[i + 1]) >= 0) {
                i--;
            }
            if (i < 0)
                return false;
            int j = _elements.Length - 1;
            while (comparer.Compare(_elements[j], _elements[i]) <= 0) {
                j--;
            }
            (_elements[i], _elements[j]) = (_elements[j], _elements[i]);
            Reverse(_elements, i + 1);
            return true;

            static void Reverse(Span<T> input, int start) {
                int i = start;
                for (var j = input.Length - 1; i < j; i += 1, j -= 1) {
                    (input[i], input[j]) = (input[j], input[i]);
                }
            }
        }
    }

    /// <summary>
    /// Enumerator that generates all permutations using Heap's algorithm.
    /// </summary>
    /// <remarks>
    /// - Produces every permutation (including duplicates if the input contains repeated elements).
    /// - Uses an internal indices array to drive in-place swaps; yields permutations as a <see cref="ReadOnlySpan{T}"/> into the internal buffer.
    /// - The enumerator is a <see langword="struct"/> and follows the foreach pattern via <see cref="GetEnumerator"/>.
    /// - Not thread-safe.
    /// </remarks>
    public struct Heaps<T> {
        private readonly T[] _elements;
        private int[]? _indices;
        private int _currIndex;
        private bool _firstEnumerator = true;

        /// <summary>
        /// Initializes a new <see cref="Heaps{T}"/> enumerator.
        /// </summary>
        /// <param name="elements">Sequence to enumerate permutations of. The sequence is copied into an internal array.</param>
        public Heaps(IEnumerable<T> elements) {
            _elements = [.. elements];
        }

        /// <summary>
        /// Returns an enumerator instance suitable for use in a foreach loop.
        /// </summary>
        /// <remarks>
        /// Returns itself on the first call and a new enumerator on subsequent calls to preserve foreach semantics for re-enumeration.
        /// </remarks>
        public Heaps<T> GetEnumerator() {
            if (_firstEnumerator) {
                _firstEnumerator = false;
                return this;
            }
            return new(_elements) { _firstEnumerator = false };
        }

        /// <summary>
        /// Gets the current permutation as a <see cref="ReadOnlySpan{T}"/> over the internal array.
        /// </summary>
        /// <remarks>
        /// The returned span references the enumerator's internal buffer which is mutated by subsequent iterations.
        /// Copy the span if you need to retain the result.
        /// </remarks>
        public readonly ReadOnlySpan<T> Current => _elements.AsSpan();

        /// <summary>
        /// Advances the enumerator to the next permutation using Heap's algorithm.
        /// </summary>
        /// <returns><see langword="true"/> if the next permutation is available; otherwise <see langword="false"/>.</returns>
        /// <remarks>
        /// The first call initializes internal indices and yields the initial permutation. Subsequent calls perform in-place swaps driven by the indices array.
        /// When all permutations have been generated, returns <see langword="false"/>.
        /// </remarks>
        public bool MoveNext() {
            var elements = _elements;
            if (_indices == null) {
                _indices = new int[elements.Length];
                _currIndex = 1;
                return true;
            }
            var currIndex = _currIndex;
            var indices = _indices;
            while (currIndex < elements.Length) {
                if (indices[currIndex] < currIndex) {
                    if (currIndex % 2 == 0) {
                        (elements[0], elements[currIndex]) = (elements[currIndex], elements[0]);
                    } else {
                        (elements[indices[currIndex]], elements[currIndex]) = (elements[currIndex], elements[indices[currIndex]]);
                    }
                    indices[currIndex] += 1;
                    _currIndex = 1;
                    return true;
                } else {
                    indices[currIndex] = 0;
                    currIndex += 1;
                }
            }
            _currIndex = currIndex;
            return false;
        }
    }
}
