using static RvB.Collections.SetsV2Enumerators;

namespace RvB.Collections;

public static class SetsV2 {
    public static SubsetsOrderInsensitive<T> SubsetsOrderInsensitive<T>(this IEnumerable<T> set, int subsetSize)
        => new(CheckAndGetArray(set, subsetSize), subsetSize);

    public static SubsetsOrderInsensitive<T> SubsetsOrderInsensitive<T>(this Span<T> set, int subsetSize)
        => new(CheckAndGetArray((ReadOnlySpan<T>)set, subsetSize), subsetSize);

    public static SubsetsOrderInsensitive<T> SubsetsOrderInsensitive<T>(this ReadOnlySpan<T> set, int subsetSize)
        => new(CheckAndGetArray(set, subsetSize), subsetSize);

    public static SubsetsRange<T> SubsetsOrderInsensitive<T>(this IEnumerable<T> set, int subsetMinSize, int subsetMaxSize) {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(subsetMinSize, nameof(subsetMinSize));
        ArgumentOutOfRangeException.ThrowIfLessThan(subsetMaxSize, subsetMinSize, nameof(subsetMaxSize));
        var array = CheckAndGetArray(set, subsetMinSize);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(subsetMaxSize, array.Length, nameof(subsetMaxSize));
        return new SubsetsRange<T>(new SubsetsOrderInsensitive<T>(array, subsetMinSize), subsetMinSize, subsetMaxSize);
    }

    public static SubsetsOrderSensitive<T> SubsetsOrderSensitive<T>(this IEnumerable<T> set, int subsetSize)
        => new(CheckAndGetArray(set, subsetSize), subsetSize);

    public static SubsetsOrderSensitive<T> SubsetsOrderSensitive<T>(this Span<T> set, int subsetSize)
        => new(CheckAndGetArray((ReadOnlySpan<T>)set, subsetSize), subsetSize);

    public static SubsetsOrderSensitive<T> SubsetsOrderSensitive<T>(this ReadOnlySpan<T> set, int subsetSize)
        => new(CheckAndGetArray(set, subsetSize), subsetSize);

    public static SubsetsRange<T> SubsetsOrderSensitive<T>(this IEnumerable<T> set, int subsetMinSize, int subsetMaxSize) {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(subsetMinSize, nameof(subsetMinSize));
        ArgumentOutOfRangeException.ThrowIfLessThan(subsetMaxSize, subsetMinSize, nameof(subsetMaxSize));
        var array = CheckAndGetArray(set, subsetMinSize);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(subsetMaxSize, array.Length, nameof(subsetMaxSize));
        return new SubsetsRange<T>(new SubsetsOrderSensitive<T>(array, subsetMinSize), subsetMinSize, subsetMaxSize);
    }

    public static SubsetsFull<T> SubsetsFull<T>(this IEnumerable<T> set, int subsetSize)
        => new(CheckAndGetArray(set, subsetSize), subsetSize);

    public static SubsetsFull<T> SubsetsFull<T>(this Span<T> set, int subsetSize)
        => new(CheckAndGetArray((ReadOnlySpan<T>)set, subsetSize), subsetSize);

    public static SubsetsFull<T> SubsetsFull<T>(this ReadOnlySpan<T> set, int subsetSize)
        => new(CheckAndGetArray(set, subsetSize), subsetSize);

    public static SubsetsRange<T> SubsetsFull<T>(this IEnumerable<T> set, int subsetMinSize, int subsetMaxSize) {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(subsetMinSize, nameof(subsetMinSize));
        ArgumentOutOfRangeException.ThrowIfLessThan(subsetMaxSize, subsetMinSize, nameof(subsetMaxSize));
        var array = CheckAndGetArray(set, subsetMinSize);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(subsetMaxSize, array.Length, nameof(subsetMaxSize));
        return new SubsetsRange<T>(new SubsetsFull<T>(array, subsetMinSize), subsetMinSize, subsetMaxSize);
    }

    private static T[] CheckAndGetArray<T>(IEnumerable<T> set, int subsetSize) {
        ArgumentNullException.ThrowIfNull(set, nameof(set));
        var array = set.ToArray();
        if (subsetSize > array.Length)
            throw new ArgumentException("Subset size is larger than set size", nameof(subsetSize));
        return array;
    }

    private static T[] CheckAndGetArray<T>(ReadOnlySpan<T> set, int subsetSize) {
        var array = set.ToArray();
        if (subsetSize > array.Length)
            throw new ArgumentException("Subset size is larger than set size", nameof(subsetSize));
        return array;
    }
}

public interface ISetEnumerator<T> {
    int Size { get; }
    ReadOnlySpan<T> Current { get; }
    bool MoveNext();
    internal ISetEnumerator<T> GetEnumerator(int subsetSize);
}

/// <summary>
/// Class containing the set enumerators. It's separated from SetsV2 so that intellisense will only show relevant methods.
/// </summary>
public static class SetsV2Enumerators {
    public struct SubsetsOrderInsensitive<T> : ISetEnumerator<T> {
        private readonly T[] _array;
        private readonly int _subsetSize;
        private readonly T[] _subset;
        private readonly int[] _indices;
        private bool _firstSet = true;
        private bool _firstEnumerator = true;

        internal SubsetsOrderInsensitive(T[] array, int subsetSize) {
            _array = array;
            _subsetSize = subsetSize;
            _subset = array[..subsetSize];
            _indices = [.. Enumerable.Range(0, subsetSize)];
        }

        public readonly int Size => _subsetSize;

        public SubsetsOrderInsensitive<T> GetEnumerator() {
            if (_firstEnumerator) {
                _firstEnumerator = false;
                return this;
            }
            return new(_array, _subsetSize) { _firstEnumerator = false };
        }

        readonly ISetEnumerator<T> ISetEnumerator<T>.GetEnumerator(int subsetSize)
            => new SubsetsOrderInsensitive<T>(_array, subsetSize);

        public readonly ReadOnlySpan<T> Current => _subset;

        public bool MoveNext() {
            if (_firstSet) {
                _firstSet = false;
                return true;
            }
            int indicesIndex = _indices.Length - 1;
            bool ok = _indices[indicesIndex] < _array.Length - 1;
            while (!ok) {
                indicesIndex -= 1;
                if (indicesIndex < 0) {
                    return false;
                }
                ok = (_indices[indicesIndex] + _indices.Length - indicesIndex < _array.Length);
            }
            _indices[indicesIndex] += 1;
            _subset[indicesIndex] = _array[_indices[indicesIndex]];
            while (indicesIndex < _indices.Length - 1) {
                var newIndex = _indices[indicesIndex] + 1;
                indicesIndex += 1;
                _indices[indicesIndex] = newIndex;
                _subset[indicesIndex] = _array[newIndex];
            }
            return true;
        }
    }

    public struct SubsetsOrderSensitive<T> : ISetEnumerator<T> {
        private readonly T[] _array;
        private readonly int _subsetSize;
        private readonly T[] _subset;
        private readonly int[] _indices;
        private readonly bool[] _usedIndices2;
        private bool _firstSet = true;
        private bool _firstEnumerator = true;

        internal SubsetsOrderSensitive(T[] array, int subsetSize) {
            _array = array;
            _subsetSize = subsetSize;

            _subset = array[..subsetSize];
            _indices = [.. Enumerable.Range(0, subsetSize)];
            _usedIndices2 = [.. Enumerable.Range(0, subsetSize).Select(_ => true)];
        }

        public readonly int Size => _subsetSize;

        public SubsetsOrderSensitive<T> GetEnumerator() {
            if (_firstEnumerator) {
                _firstEnumerator = false;
                return this;
            }
            return new(_array, _subsetSize) { _firstEnumerator = false };
        }

        readonly ISetEnumerator<T> ISetEnumerator<T>.GetEnumerator(int subsetSize)
            => new SubsetsOrderSensitive<T>(_array, subsetSize);

        public readonly ReadOnlySpan<T> Current => _subset;

        public bool MoveNext() {
            if (_firstSet) {
                _firstSet = false;
                return true;
            }
            int indicesIndex = _indices.Length - 1;
            int nextIndex;
            while (true) {
                _usedIndices2[_indices[indicesIndex]] = false;
                nextIndex = _indices[indicesIndex] + 1;
                while (nextIndex < _array.Length && _usedIndices2[nextIndex]) {
                    nextIndex += 1;
                }
                if (nextIndex < _array.Length) {
                    _indices[indicesIndex] = nextIndex;
                    _subset[indicesIndex] = _array[nextIndex];
                    _usedIndices2[nextIndex] = true;
                    break;
                }
                indicesIndex -= 1;
                if (indicesIndex < 0) {
                    return false;
                }
            }
            nextIndex = 0;
            while (indicesIndex < _indices.Length - 1) {
                indicesIndex += 1;
                while (_usedIndices2[nextIndex]) {
                    nextIndex += 1;
                }
                _indices[indicesIndex] = nextIndex;
                _subset[indicesIndex] = _array[nextIndex];
                _usedIndices2[nextIndex] = true;
                nextIndex += 1;
            }
            return true;
        }
    }

    public struct SubsetsFull<T> : ISetEnumerator<T> {
        private readonly T[] _array;
        private readonly int _subsetSize;
        private readonly T[] _subset;
        private readonly int[] _indices;
        private bool _firstSet = true;
        private bool _firstEnumerator = true;

        public readonly int Size => _subsetSize;

        internal SubsetsFull(T[] array, int subsetSize) {
            _array = array;
            _subsetSize = subsetSize;
            _subset = [.. Enumerable.Range(0, subsetSize).Select(_ => array[0])];
            _indices = new int[_subsetSize];
        }

        public SubsetsFull<T> GetEnumerator() {
            if (_firstEnumerator) {
                _firstEnumerator = false;
                return this;
            }
            return new(_array, _subsetSize) { _firstEnumerator = false };
        }

        readonly ISetEnumerator<T> ISetEnumerator<T>.GetEnumerator(int subsetSize)
            => new SubsetsFull<T>(_array, subsetSize);

        public readonly ReadOnlySpan<T> Current => _subset;

        public bool MoveNext() {
            if (_firstSet) {
                _firstSet = false;
                return true;
            }
            var index = _indices.Length - 1;
            while (index >= 0 && _indices[index] >= _array.Length - 1) {
                _indices[index] = 0;
                _subset[index] = _array[0];
                index -= 1;
            }
            if (index < 0) {
                return false;
            }
            _indices[index] += 1;
            _subset[index] = _array[_indices[index]];
            return true;
        }
    }

    public struct SubsetsRange<T> : ISetEnumerator<T> {
        private int _subsetSize;
        private readonly int _subsetMinSize;
        private readonly int _subsetMaxSize;
        private bool _firstEnumerator = true;
        private ISetEnumerator<T> _enumerator;

        internal SubsetsRange(ISetEnumerator<T> enumerator, int subsetMinSize, int subsetMaxSize) {
            _enumerator = enumerator;
            _subsetSize = _subsetMinSize = subsetMinSize;
            _subsetMaxSize = subsetMaxSize;
        }

        public readonly int Size => _subsetSize;

        public SubsetsRange<T> GetEnumerator() {
            if (_firstEnumerator) {
                _firstEnumerator = false;
                return this;
            }
            return new(_enumerator, _subsetMinSize, _subsetMaxSize) { _firstEnumerator = false };
        }

        readonly ISetEnumerator<T> ISetEnumerator<T>.GetEnumerator(int subsetSize)
            => throw new NotSupportedException();

        public readonly ReadOnlySpan<T> Current => _enumerator.Current;

        public bool MoveNext() {
            if (_enumerator.MoveNext()) {
                return true;
            }
            if (_subsetSize == _subsetMaxSize) {
                return false;
            }
            _subsetSize += 1;
            _enumerator = _enumerator.GetEnumerator(_subsetSize);
            return _enumerator.MoveNext();
        }
    }
}

public static class SetEnumeratorExtensions {
    public static List<T[]> ToList<T>(this ISetEnumerator<T> enumerator) {
        var list = new List<T[]>();
        while (enumerator.MoveNext()) {
            var result = new T[enumerator.Size];
            enumerator.Current.CopyTo(result.AsSpan());
            list.Add(result);
        }
        return list;
    }

    public static T[][] ToArray<T>(this ISetEnumerator<T> enumerator) {
        return [.. ToList(enumerator)];
    }
}
