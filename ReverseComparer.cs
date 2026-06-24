namespace RvB.Collections;

public sealed class ReverseComparer<T> : IComparer<T> {
    private readonly IComparer<T> _originalComparer;

    public static ReverseComparer<T> Default { get; } = new ReverseComparer<T>();

    public ReverseComparer() : this(Comparer<T>.Default) { }

    public ReverseComparer(IComparer<T> originalComparer) {
        _originalComparer = originalComparer;
    }
    public int Compare(T? x, T? y) {
        return _originalComparer.Compare(y, x);
    }
}
