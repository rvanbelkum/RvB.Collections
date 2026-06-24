using System.Collections;

namespace RvB.Collections;

public class RoaringBitmap : IEnumerable<int>, IEquatable<RoaringBitmap> {
    private readonly RoaringArray _highLowContainer;

    private RoaringBitmap(RoaringArray input) {
        _highLowContainer = input;
    }

    public long Cardinality => _highLowContainer.Cardinality;

    public bool Contains(int x) {
        ushort hb = Util.HighBits(x);
        var c = _highLowContainer.GetContainer(hb);
        return c != null && c.Contains(Util.LowBits(x));
    }

    public IEnumerator<int> GetEnumerator()
        => _highLowContainer.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();

    public bool Equals(RoaringBitmap? other) {
        if (ReferenceEquals(this, other)) {
            return true;
        }
        if (other is null) {
            return false;
        }
        return _highLowContainer.Equals(other._highLowContainer);
    }

    /// <summary>
    /// Creates a new immutable RoaringBitmap from an existing list of integers
    /// </summary>
    /// <param name="values">List of integers</param>
    /// <returns>RoaringBitmap</returns>
    public static RoaringBitmap Create(params int[] values) {
        return Create(values.AsEnumerable());
    }

    /// <summary>
    /// Optimizes a RoaringBitmap to prepare e.g. for Serialization/Deserialization
    /// </summary>
    /// <returns>RoaringBitmap</returns>
    public RoaringBitmap Optimize() {
        return new RoaringBitmap(RoaringArray.Optimize(_highLowContainer));
    }

    /// <summary>
    /// Creates a new immutable RoaringBitmap from an existing list of integers
    /// </summary>
    /// <param name="values">List of integers</param>
    /// <returns>RoaringBitmap</returns>
    public static RoaringBitmap Create(IEnumerable<int> values) {
        var groupbyHb = values.Distinct().OrderBy(t => t).GroupBy(Util.HighBits).OrderBy(t => t.Key);
        var keys = new List<ushort>();
        var containers = new List<Container>();
        var size = 0;
        foreach (var group in groupbyHb) {
            keys.Add(group.Key);
            if (group.Count() > Container.MaxSize) {
                containers.Add(BitmapContainer.Create(group.Select(Util.LowBits).ToArray()));
            } else {
                containers.Add(ArrayContainer.Create(group.Select(Util.LowBits).ToArray()));
            }
            size++;
        }
        return new RoaringBitmap(new RoaringArray(size, keys, containers));
    }

    /// <summary>
    /// Bitwise Or operation of two RoaringBitmaps
    /// </summary>
    /// <param name="x">RoaringBitmap</param>
    /// <param name="y">RoaringBitmap</param>
    /// <returns>RoaringBitmap</returns>
    public static RoaringBitmap operator |(RoaringBitmap x, RoaringBitmap y) {
        return new RoaringBitmap(x._highLowContainer | y._highLowContainer);
    }

    /// <summary>
    /// Bitwise And operation of two RoaringBitmaps
    /// </summary>
    /// <param name="x">RoaringBitmap</param>
    /// <param name="y">RoaringBitmap</param>
    /// <returns>RoaringBitmap</returns>
    public static RoaringBitmap operator &(RoaringBitmap x, RoaringBitmap y) {
        return new RoaringBitmap(x._highLowContainer & y._highLowContainer);
    }

    /// <summary>
    /// Bitwise Not operation of a RoaringBitmap
    /// </summary>
    /// <param name="x">RoaringBitmap</param>
    /// <returns>RoaringBitmap</returns>
    public static RoaringBitmap operator ~(RoaringBitmap x) {
        return new RoaringBitmap(~x._highLowContainer);
    }

    /// <summary>
    /// Bitwise Xor operation of two RoaringBitmaps
    /// </summary>
    /// <param name="x">RoaringBitmap</param>
    /// <param name="y">RoaringBitmap</param>
    /// <returns>RoaringBitmap</returns>
    public static RoaringBitmap operator ^(RoaringBitmap x, RoaringBitmap y) {
        return new RoaringBitmap(x._highLowContainer ^ y._highLowContainer);
    }

    /// <summary>
    /// Bitwise AndNot operation of two RoaringBitmaps
    /// </summary>
    /// <param name="x">RoaringBitmap</param>
    /// <param name="y">RoaringBitmap</param>
    /// <returns>RoaringBitmap</returns>
    public static RoaringBitmap AndNot(RoaringBitmap x, RoaringBitmap y) {
        return new RoaringBitmap(RoaringArray.AndNot(x._highLowContainer, y._highLowContainer));
    }

    public override bool Equals(object? obj) {
        return obj is RoaringArray ra && Equals(ra);
    }

    public override int GetHashCode() {
        return (13 ^ _highLowContainer.GetHashCode()) << 3;
    }

    /// <summary>
    /// Serializes a RoaringBitmap into a stream using the 'official' RoaringBitmap file format
    /// </summary>
    /// <param name="roaringBitmap">RoaringBitmap</param>
    /// <param name="stream">Stream</param>
    public static void Serialize(RoaringBitmap roaringBitmap, Stream stream) {
        RoaringArray.Serialize(roaringBitmap._highLowContainer, stream);
    }

    /// <summary>
    /// Deserializes a RoaringBitmap from astream using the 'official' RoaringBitmap file format
    /// </summary>
    /// <param name="stream">Stream</param>
    public static RoaringBitmap Deserialize(Stream stream) {
        var ra = RoaringArray.Deserialize(stream);
        return new RoaringBitmap(ra);
    }
}