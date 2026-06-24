using System.Buffers;
using System.Runtime.CompilerServices;

namespace RvB.Collections;

public ref struct ArrayBuilder<T> : IDisposable {
    public const int MaxLength = 0X7FFFFFC7;
    private const int InitialBufferLength = 32;

    private InlineArray32<T> _initialBuffer = default;
    private InlineArray25<T[]> _segments; // rented buffers
    private int _segmentsCount;
    private Span<T> _currentSegment;
    private int _currentSegmentIndex;
    private int _currentSegmentLength;
    private int _totalCount;
    private bool _isDisposed = false;

    public ArrayBuilder() {
        _currentSegmentLength = InitialBufferLength;
    }

    public readonly int Count => _totalCount;

    public void Add(T item) {
        if (_totalCount == MaxLength) {
            throw new OutOfMemoryException();
        }
        if (_segmentsCount == 0) {
            AddItem(_initialBuffer, ref item);
        } else {
            AddItem(_currentSegment, ref item);
        }
    }

    public void AddRange(Span<T> range) {
        while (!range.IsEmpty) {
            var itemsToCopy = int.Min(range.Length, _currentSegmentLength - _currentSegmentIndex);
            if (_segmentsCount == 0) {
                AddItems(_initialBuffer, range[..itemsToCopy]);
            } else {
                AddItems(_currentSegment, range[..itemsToCopy]);
            }
            range = range.Slice(itemsToCopy);
            if (!range.IsEmpty) {
                CreateNewSegment();
            }
        }
    }

    private void AddItem(scoped Span<T> segment, ref T value) {
        if (_currentSegmentIndex == _currentSegmentLength) {
            segment = CreateNewSegment();
        }
        segment[_currentSegmentIndex++] = value;
        _totalCount += 1;
    }

    private void AddItems(scoped Span<T> segment, Span<T> values) {
        values.CopyTo(segment.Slice(_currentSegmentIndex));
        _currentSegmentIndex += values.Length;
        _totalCount += values.Length;
    }

    public void Dispose() {
        if (!_isDisposed) {
            for (var segmentIndex = 0; segmentIndex < _segmentsCount; segmentIndex++) {
                ArrayPool<T>.Shared.Return(_segments[segmentIndex], clearArray: RuntimeHelpers.IsReferenceOrContainsReferences<T>());
            }
            _segmentsCount = _currentSegmentIndex = _totalCount = 0;
            _isDisposed = true;
        }
    }

    public T[] ToArray(bool rented = false) {
        T[] array;
        if (rented) {
            array = ArrayPool<T>.Shared.Rent(_totalCount);
        } else {
            array = GC.AllocateUninitializedArray<T>(_totalCount);
        }
        CopyTo(array);
        return array;
    }

    public void CopyTo(Span<T> span) {
        if (_segmentsCount == 0) {
            Span<T> initSpan = _initialBuffer;
            initSpan.Slice(0, _currentSegmentIndex).CopyTo(span);
        } else {
            Span<T> initSpan = _initialBuffer;
            initSpan.CopyTo(span);
            var offset = InitialBufferLength;
            for (var segmentIndex = 0; segmentIndex < _segmentsCount - 1; segmentIndex++) {
                _segments[segmentIndex].CopyTo(span.Slice(offset));
                offset += _segments[segmentIndex].Length;
            }
            _segments[_segmentsCount - 1].AsSpan().Slice(0, _currentSegmentIndex).CopyTo(span.Slice(offset));
        }
    }

    private Span<T> CreateNewSegment() {
        _currentSegmentLength = (int)Math.Min(_currentSegmentLength * 2L, MaxLength);
        _currentSegment = _segments[_segmentsCount] = ArrayPool<T>.Shared.Rent(_currentSegmentLength);
        _currentSegmentIndex = 0;
        _segmentsCount += 1;
        return _currentSegment;
    }
}

[InlineArray(25)]
internal struct InlineArray25<T> {
    T _item;
}

[InlineArray(32)]
internal struct InlineArray32<T> {
    T _item;
}
