using System.Diagnostics.CodeAnalysis;

namespace RvB.Collections;

public sealed class SortedSet<TKey, TValue> {
    private readonly SortedSet<(TKey Key, TValue Value)> _sortedSet;

    class KeyValueComparer : IComparer<(TKey Key, TValue)> {
        private readonly IComparer<TKey> _comparer;
        public static KeyValueComparer Default { get; } = new();
        public KeyValueComparer(IComparer<TKey>? comparer = null) {
            _comparer = comparer ?? Comparer<TKey>.Default;
        }
        public int Compare((TKey Key, TValue) x, (TKey Key, TValue) y) => _comparer.Compare(x.Key, y.Key);
    }

    public SortedSet(IComparer<TKey>? comparer) {
        KeyValueComparer keyValueComparer = comparer == null ? KeyValueComparer.Default : new KeyValueComparer(comparer);
        _sortedSet = new(keyValueComparer);
    }

    public SortedSet() : this((IComparer<TKey>?)null) { }

    public SortedSet(IEnumerable<(TKey, TValue)> collection) : this(collection, null) { }

    public SortedSet(IEnumerable<(TKey, TValue)> collection, IComparer<TKey>? comparer) {
        KeyValueComparer keyValueComparer = comparer == null ? KeyValueComparer.Default : new KeyValueComparer(comparer);
        _sortedSet = new(collection, keyValueComparer);
    }

    public bool Add(TKey key, TValue value) {
        return _sortedSet.Add((key, value));
    }

    public bool ContainsKey(TKey key) {
        return _sortedSet.Contains((key, default!));
    }

    public bool Contains(TKey key, TValue value, IEqualityComparer<TValue>? valueComparer) {
        if (TryGetValue(key, out var existingValue)) {
            valueComparer ??= EqualityComparer<TValue>.Default;
            return valueComparer.Equals(value, existingValue);
        }
        return false;
    }

    public bool Contains(TKey key, TValue value) {
        return Contains(key, value, null);
    }

    public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value) {
        if (_sortedSet.TryGetValue((key, default!), out var keyValue)) {
            value = keyValue.Value;
            return true;
        }
        value = default;
        return false;
    }
}
