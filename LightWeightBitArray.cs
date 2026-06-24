using System.Numerics;
using System.Runtime.CompilerServices;

namespace RvB.Collections;

using Entry = uint; // Can be byte, ushort, uint, or ulong depending on the desired balance between memory usage and performance.
                    // Using uint as a good default for many scenarios.

public class LightWeightBitArray {
    private const string IndexOutOfRangeMessage = "Index was out of range. Must be less than the size of the collection.";
    private const uint BitsPerEntry = sizeof(Entry) << 3;

    private readonly Entry[] _array;
    private readonly ulong _bitLength;
    private readonly Entry _extraBitsMask;

    public ulong Length => _bitLength;

    public int Count() {
        var count = 0;
        foreach (var item in _array) {
            count += BitOperations.PopCount(item);
        }
        return count;
    }

    public static ulong MaxLength => (ulong)Array.MaxLength * BitsPerEntry;

    public bool this[int index] {
        get => Get(index);
        set => Set(index, value);
    }

    public bool this[ulong index] {
        get => Get(index);
        set => Set(index, value);
    }

    public LightWeightBitArray(int length) : this((ulong)length) { }

    public LightWeightBitArray(ulong length) {
        _array = AllocateArray(length);
        _bitLength = length;
        var extraBits = (uint)_bitLength & 0x07;
        _extraBitsMask = (Entry)((1 << (int)extraBits) - 1);
    }

    public LightWeightBitArray(LightWeightBitArray other) {
        _array = AllocateArray(other.Length);
        other._array.CopyTo(_array, 0);
        _bitLength = other.Length;
        _extraBitsMask = other._extraBitsMask;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Get(int index)
        => Get((ulong)index);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Get(ulong index) {
        if (index >= _bitLength) {
            throw new ArgumentOutOfRangeException(nameof(index), index, IndexOutOfRangeMessage);
        }
        var (entryIndex, bitOffset) = Math.DivRem(index, BitsPerEntry);
        return (_array[entryIndex] & (Entry)(1 << (int)bitOffset)) != 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Set(int index, bool value)
        => Set((ulong)index, value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Set(ulong index, bool value) {
        if (index >= _bitLength) {
            throw new ArgumentOutOfRangeException(nameof(index), index, IndexOutOfRangeMessage);
        }
        var (entryIndex, bitOffset) = Math.DivRem(index, BitsPerEntry);
        ref Entry segment = ref _array[entryIndex];
        var bitMask = (Entry)(1 << (int)bitOffset);
        if (value) {
            segment |= bitMask;
        } else {
#pragma warning disable IDE0004 // Cast is redundant
            segment &= (Entry)~bitMask;
#pragma warning restore IDE0004 // Cast is redundant
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool SetIfNotSet(int index)
        => SetIfNotSet((ulong)index);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool SetIfNotSet(ulong index) {
        if (index >= _bitLength) {
            throw new ArgumentOutOfRangeException(nameof(index), index, IndexOutOfRangeMessage);
        }
        var (entryIndex, bitOffset) = Math.DivRem(index, BitsPerEntry);
        ref Entry segment = ref _array[entryIndex];
        var oldSegment = segment;
        var bitMask = (Entry)(1 << (int)bitOffset);
        segment |= bitMask;
        return oldSegment != segment;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Invert(ulong index) {
        if (index >= _bitLength) {
            throw new ArgumentOutOfRangeException(nameof(index), index, IndexOutOfRangeMessage);
        }
        var (entryIndex, bitOffset) = Math.DivRem(index, BitsPerEntry);
        ref Entry segment = ref _array[entryIndex];
        var bitMask = (Entry)(1 << (int)bitOffset);
        segment ^= bitMask;
    }

    public void SetAll(bool value) {
        if (value) {
            _array.AsSpan().Fill(0xFF);
            // Clear any extra bits
            if (_extraBitsMask != 0) {
                _array[^1] = _extraBitsMask;
            }
        } else {
            _array.AsSpan().Clear();
        }
    }

    public bool HasAllSet() {
        var array = _array;
        var extraBitsMask = _extraBitsMask;
        if (extraBitsMask == 0) {
            return array.AsSpan().ContainsAnyExcept((byte)0xFF);
        }
        if (array.AsSpan(0, array.Length - 1).ContainsAnyExcept((byte)0xFF))
            return false;
        return (array[^1] & extraBitsMask) == extraBitsMask;
    }

    public bool HasAnySet() {
        return _array.AsSpan().ContainsAnyExcept((Entry)0);
    }

    public LightWeightBitArray Or(LightWeightBitArray value) {
        if (value is null) {
            throw new ArgumentException("Cannot Or BitArrays of different lengths", nameof(value));
        }
        Entry[] minArray;
        LightWeightBitArray max;
        if (Length < value.Length) {
            minArray = _array;
            max = value;
        } else {
            minArray = value._array;
            max = this;
        }
        var result = new LightWeightBitArray(max);
        var dest = result._array;
        for (var i = 0; i < minArray.Length; i++) {
            dest[i] |= minArray[i];
        }
        return result;
    }

    public Enumerator GetEnumerator() => new(_array);

    public struct Enumerator {
        private readonly Entry[] _array;
        private uint _index;
        private ulong _value;
        private Entry _item;
        private readonly int _bitsShift;

        public Enumerator(Entry[] array) {
            _array = array;
            if (array.Length > 0)
                _item = array[0];
            _bitsShift = BitOperations.Log2(BitsPerEntry);
        }

        public ulong Current { get; private set; }

        public bool TryGetNext(out ulong next) {
            if (MoveNext()) {
                next = Current;
                return true;
            }
            next = default;
            return false;
        }

        public bool MoveNext() {
            var item = _item;
            var value = _value;
            if (item == 0) {
                var index = _index;
                while (item == 0 && index < _array.Length - 1) {
                    item = _array[++index];
                }
                value = (ulong)index << _bitsShift;
                _index = index;
            }
            if (item > 0) {
                var offset = (byte)BitOperations.TrailingZeroCount(item);
                value += offset;
                Current = value;
                _value = value + 1;
                if (offset == BitsPerEntry - 1) {
                    _item = 0;
                } else {
#pragma warning disable IDE0004 // Cast is redundant
                    _item = (Entry)(item >> (offset + 1));
#pragma warning restore IDE0004 // Cast is redundant
                }
                return true;
            }
            return false;
        }
    }

    private static Entry[] AllocateArray(ulong bitLength) {
        if (bitLength == 0)
            return [];
        var (entryLength, bitsleft) = Math.DivRem(bitLength, BitsPerEntry);
        if (bitsleft > 0) {
            entryLength += 1;
        }
        if (entryLength > (uint)Array.MaxLength) {
            throw new ArgumentException("Size too big", nameof(bitLength));
        }
        return new Entry[entryLength];
    }
}
