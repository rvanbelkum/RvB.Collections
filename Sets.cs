using System.Collections;

namespace RvB.Collections;

public struct SubSet<T> : IEnumerable<T> {
    private T[] _subset;

    public SubSet(T[] subset) {
        _subset = subset;
    }

    public int Length => _subset.Length;

    public T this[int index] => _subset[index];

    public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)_subset).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

public static class Sets {
    public enum SetType {
        /// <summary>
        /// Order of the set is not significant: [1,2] is equivalent to [2,1]
        /// </summary>
        OrderInsensitive,
        /// <summary>
        /// Order of the set is significant: [1,2] is different from [2,1]
        /// </summary>
        OrderSensitive,
        Full,
    }

    /// <summary>
    /// Generates subsets of a given size from a bigger set.
    /// </summary>
    /// <typeparam name="T">The type of the elements in the set</typeparam>
    /// <param name="set">Original set</param>
    /// <param name="subsetSize">Size of the subsets generated</param>
    /// <param name="type">Type of subsets to be generated</param>
    /// <returns></returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="subsetSize"/> is less than 1</exception>
    public static IEnumerable<SubSet<T>> Subsets<T>(IEnumerable<T> set, int subsetSize, SetType type) {
        if (subsetSize < 1)
            throw new ArgumentException("Subset size needs to be at least 1", nameof(subsetSize));
        return type switch {
            SetType.OrderInsensitive => SubsetsOrderInsensitive(set, subsetSize),
            SetType.OrderSensitive => SubsetsOrderSensitive(set, subsetSize),
            SetType.Full => SubsetsFull(set, new T[subsetSize], 0),
            _ => throw new NotImplementedException(),
        };
    }


    private static IEnumerable<SubSet<T>> SubsetsOrderInsensitive<T>(IEnumerable<T> set, int subsetSize) {
        var setArray = set.ToArray();
        if (subsetSize > setArray.Length)
            throw new ArgumentException("Subset size is larger than set size", nameof(subsetSize));

        var subset = new T[subsetSize];
        var indices = new int[subsetSize];

        for (var i = 0; i < indices.Length; ++i) {
            indices[i] = i;
            subset[i] = setArray[indices[i]];
        }
        while (true) {
            yield return new(subset);

            int indicesIndex = indices.Length - 1;
            bool ok = indices[indicesIndex] < setArray.Length - 1;
            while (!ok) {
                indicesIndex -= 1;
                if (indicesIndex < 0)
                    yield break;
                ok = (indices[indicesIndex] + indices.Length - indicesIndex < setArray.Length);
            }
            indices[indicesIndex] += 1;
            subset[indicesIndex] = setArray[indices[indicesIndex]];
            while (indicesIndex < indices.Length - 1) {
                var newIndex = indices[indicesIndex] + 1;
                indicesIndex += 1;
                indices[indicesIndex] = newIndex;
                subset[indicesIndex] = setArray[newIndex];
            }
        }
    }

    private static IEnumerable<SubSet<T>> SubsetsOrderSensitive<T>(IEnumerable<T> set, int subsetSize) {
        var setArray = set.ToArray();
        if (subsetSize > setArray.Length)
            throw new ArgumentException("Subset size is larger than set size", nameof(subsetSize));

        var subset = new T[subsetSize];
        var indices = new int[subsetSize];
        HashSet<int> usedIndices = [];

        for (var i = 0; i < indices.Length; ++i) {
            indices[i] = i;
            subset[i] = setArray[indices[i]];
            usedIndices.Add(i);
        }
        while (true) {
            yield return new(subset);

            int indicesIndex = indices.Length - 1;
            int nextIndex;
            while (true) {
                usedIndices.Remove(indices[indicesIndex]);
                nextIndex = indices[indicesIndex] + 1;
                while (usedIndices.Contains(nextIndex))
                    nextIndex += 1;
                if (nextIndex < setArray.Length) {
                    indices[indicesIndex] = nextIndex;
                    subset[indicesIndex] = setArray[nextIndex];
                    usedIndices.Add(nextIndex);
                    break;
                }
                indicesIndex -= 1;
                if (indicesIndex < 0)
                    yield break;
            }
            nextIndex = 0;
            while (indicesIndex < indices.Length - 1) {
                indicesIndex += 1;
                while (usedIndices.Contains(nextIndex))
                    nextIndex += 1;
                indices[indicesIndex] = nextIndex;
                subset[indicesIndex] = setArray[nextIndex];
                usedIndices.Add(nextIndex);
                nextIndex += 1;
            }
        }
    }

    private static IEnumerable<SubSet<T>> SubsetsFull<T>(IEnumerable<T> set, T[] subset, int index) {
        foreach (var e in set) {
            subset[index] = e;
            if (index == subset.Length - 1) {
                yield return new(subset);
            } else {
                foreach (var ss in SubsetsFull(set, subset, index + 1)) {
                    yield return ss;
                }
            }
        }
    }

    private static IEnumerable<T[]> SubsetsOrderSensitive<T>(IEnumerable<T> set, T[] subset, int index, HashSet<T> exclude) {
        foreach (var e in set) {
            if (exclude.Add(e)) {
                subset[index] = e;
                if (index == subset.Length - 1)
                    yield return subset;
                else {
                    foreach (var ss in SubsetsOrderSensitive(set, subset, index + 1, exclude)) {
                        yield return ss;
                    }
                }
                exclude.Remove(e);
            }
        }
    }

    public static IEnumerable<IEnumerable<T>> Subsets<T>(
            IEnumerable<T> set,
            int n,
            Func<T, T, bool>? successorPredicate = null
        ) where T : IComparable<T> {

        if (n == 1) {
            foreach (var e in set)
                yield return new T[] { e };
        } else {
            foreach (var e in set) {
                foreach (var ss in Subsets(set.Where(elt => successorPredicate == null || successorPredicate(e, elt)), n - 1, successorPredicate)) {
                    yield return ss.Prepend(e);
                }
            }
        }
    }

    public static IEnumerable<IEnumerable<T>> Subsets<T>(
            IEnumerable<T> set,
            int n,
            Func<T, T, bool>? successorPredicate = null,
            Func<T[], bool>? subsetPredicate = null
        ) where T : IComparable<T> {

        var setArray = set.ToArray();
        if (n > setArray.Length)
            yield break;

        var resultSetIndex = new int[n];
        for (int i = 0; i < resultSetIndex.Length; ++i) {
            resultSetIndex[i] = 0;
        }

        var result = new T[n];
        var resultIdx = 0;
        while (resultIdx >= 0) {
            var value = setArray[resultSetIndex[resultIdx]];
            bool valid = true;
            bool increase = false;
            if (resultIdx > 0 && successorPredicate != null)
                valid = successorPredicate(result[resultIdx - 1], value);
            if (valid) {
                result[resultIdx] = value;
                valid = (subsetPredicate == null || subsetPredicate(result[0..(resultIdx + 1)]));
            }
            if (valid) {
                if (resultIdx == n - 1) {
                    yield return result;
                    increase = true;
                } else {
                    resultIdx += 1;
                }
            }
            if (!valid || increase) {
                while (resultIdx >= 0 && ++resultSetIndex[resultIdx] == setArray.Length) {
                    resultSetIndex[resultIdx] = 0;
                    resultIdx--;
                }
                //if (resultIdx < 0)
                //    yield break;
            }
        }
    }
}
