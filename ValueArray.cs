using System.Collections;

namespace RvB.Collections;

public sealed class ValueArray<T> : IEnumerable<T>, IReadOnlyList<T>, IEquatable<ValueArray<T>> {
    private readonly T[] _values;

    public int Length => _values.Length;

    public int Count => _values.Length;

    public T this[int index] {
        get => _values[index];
        set => _values[index] = value;
    }

    public ValueArray(int size) {
        _values = new T[size];
    }

    public ValueArray(T[] values) {
        _values = values;
    }

    public ValueArray(IEnumerable<T> values) {
        _values = [.. values];
    }

    public ValueArray(ReadOnlySpan<T> values) {
        _values = [.. values];
    }

    public bool Equals(ValueArray<T>? other)
        => other is not null && _values.SequenceEqual(other._values);

    public override bool Equals(object? obj)
        => obj is ValueArray<T> array && Equals(array);

    public override int GetHashCode() {
        int hashCode = 0;
        foreach (var value in _values) {
            hashCode = HashCode.Combine(hashCode, value);
        }
        return hashCode;
    }

    public override string ToString() => string.Join(", ", _values);

    public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)_values).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => _values.GetEnumerator();

    public static bool operator ==(ValueArray<T>? left, ValueArray<T>? right)
        => left is null ? right == null : left.Equals(right);

    public static bool operator !=(ValueArray<T>? left, ValueArray<T>? right)
        => !(left == right);
}
