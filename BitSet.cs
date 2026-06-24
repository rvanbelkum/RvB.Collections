using System.Collections;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace RvB.Collections;

public record struct BitSet32 : IEnumerable<int> {
    private uint _items;

    public BitSet32(uint items) {
        _items = items;
    }

    public BitSet32(IEnumerable<bool> bits) {
        var value = 1u;
        var items = 0u;
        foreach (var b in bits) {
            if (b) {
                items |= value;
            }
            value <<= 1;
        }
        _items = items;
    }

    public readonly int Count => BitOperations.PopCount(_items);

    public void Add(int index) {
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, 32);
        _items |= 1u << index;
    }

    public void Add(uint values)
        => _items |= values;

    public bool TryAdd(int index) {
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, 32);
        var value = 1u << index;
        if ((_items & value) != 0) {
            return false;
        }
        _items |= value;
        return true;
    }

    public void Remove(int index)
        => _items &= ~(1u << index);

    public readonly bool Contains(int index)
        => (_items & (1u << index)) != 0;

    public readonly BitSet32 Subtract(BitSet32 bitSet)
        => new(_items & ~bitSet._items);

    public readonly BitSet32 Union(BitSet32 bitSet)
        => new(_items | bitSet._items);

    public readonly BitSet32 Intersect(BitSet32 bitSet)
        => new(_items & bitSet._items);

    public readonly int First => BitOperations.TrailingZeroCount(_items);

    public readonly IEnumerator<int> GetEnumerator()
        => BitSet.GetEnumerator(_items);

    public readonly void Deconstruct(out int index1, out int index2)
        => BitSet.Deconstruct(_items, out index1, out index2);

    public readonly void Deconstruct(out int index1, out int index2, out int index3)
        => BitSet.Deconstruct(_items, out index1, out index2, out index3);

    public readonly void Deconstruct(out int index1, out int index2, out int index3, out int index4)
        => BitSet.Deconstruct(_items, out index1, out index2, out index3, out index4);

    public override readonly string ToString()
        => string.Join(',', this);

    readonly IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public static explicit operator uint(BitSet32 bitSet) => bitSet._items;

    public static implicit operator BitSet32(uint items) => new(items);
}

public record struct BitSet64 : IEnumerable<int> {
    private ulong _items;

    public BitSet64(ulong items) {
        _items = items;
    }

    public BitSet64(int index) {
        Add(index);
    }

    public BitSet64(IEnumerable<bool> bits) {
        var value = 1ul;
        var items = 0ul;
        foreach (var b in bits) {
            if (b) {
                items |= value;
            }
            value <<= 1;
        }
        _items = items;
    }
    
    public readonly int Count => BitOperations.PopCount(_items);

    public void Add(int index) {
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, 64);
        _items |= 1u << index;
    }

    public void Add(ulong values)
        => _items |= values;

    public bool TryAdd(int index) {
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, 64);
        var value = 1u << index;
        if ((_items & value) != 0) {
            return false;
        }
        _items |= value;
        return true;
    }

    public void Remove(int index)
        => _items &= ~(1u << index);

    public readonly bool Contains(int index)
        => (_items & (1u << index)) != 0;

    public readonly BitSet64 Subtract(BitSet64 bitSet)
        => new(_items & ~bitSet._items);

    public readonly BitSet64 Union(BitSet64 bitSet)
        => new(_items | bitSet._items);

    public readonly BitSet64 Intersect(BitSet64 bitSet)
        => new(_items & bitSet._items);

    public readonly int First => BitOperations.TrailingZeroCount(_items);

    public readonly IEnumerator<int> GetEnumerator() 
        => BitSet.GetEnumerator(_items);

    public readonly void Deconstruct(out int index1, out int index2)
        => BitSet.Deconstruct(_items, out index1, out index2);

    public readonly void Deconstruct(out int index1, out int index2, out int index3)
        => BitSet.Deconstruct(_items, out index1, out index2, out index3);

    public readonly void Deconstruct(out int index1, out int index2, out int index3, out int index4)
        => BitSet.Deconstruct(_items, out index1, out index2, out index3, out index4);

    public override readonly string ToString()
        => string.Join(',', this);

    readonly IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public static explicit operator ulong(BitSet64 bitSet) => bitSet._items;

    public static implicit operator BitSet64(ulong items) => new(items);

    public static implicit operator BitSet64(int value) => new(value);
}

internal static class BitSet {
    public static void ThrowException() => throw new InvalidOperationException("BitSet does not have enough elements for deconstruction");

    public static void Deconstruct(ulong items, out int index1, out int index2, out int index3, out int index4) {
        var count = BitOperations.PopCount(items);
        if (count != 4)
            ThrowException();
        var bits = BitOperations.TrailingZeroCount(items);
        index1 = bits;
        items >>= (bits + 1);
        bits = BitOperations.TrailingZeroCount(items);
        index2 = index1 + 1 + bits;
        items >>= (bits + 1);
        bits = BitOperations.TrailingZeroCount(items);
        index3 = index2 + 1 + bits;
        items >>= (bits + 1);
        bits = BitOperations.TrailingZeroCount(items);
        index4 = index3 + 1 + bits;
    }

    public static void Deconstruct(ulong items, out int index1, out int index2, out int index3) {
        var count = BitOperations.PopCount(items);
        if (count != 3)
            ThrowException();
        var bits = BitOperations.TrailingZeroCount(items);
        index1 = bits;
        items >>= (bits + 1);
        bits = BitOperations.TrailingZeroCount(items);
        index2 = index1 + 1 + bits;
        items >>= (bits + 1);
        bits = BitOperations.TrailingZeroCount(items);
        index3 = index2 + 1 + bits;
    }

    public static void Deconstruct(ulong items, out int index1, out int index2) {
        var count = BitOperations.PopCount(items);
        if (count != 2)
            ThrowException();
        var bits = BitOperations.TrailingZeroCount(items);
        index1 = bits;
        items >>= (bits + 1);
        bits = BitOperations.TrailingZeroCount(items);
        index2 = index1 + 1 + bits;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IEnumerator<int> GetEnumerator(ulong items) {
        var value = 0;
        while (items > 0) {
            var offset = BitOperations.TrailingZeroCount(items);
            value += offset;
            yield return value;
            value += 1;
            items >>= (offset + 1);
        }
    }
}
