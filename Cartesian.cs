namespace RvB.Collections;

public static class Cartesian {
    public static IEnumerable<(T1 item1, T2 item2)> Product<T1, T2>(IEnumerable<T1> items1, IEnumerable<T2> items2) {
        foreach (var item1 in items1) {
            foreach (var item2 in items2) {
                yield return (item1, item2);
            }
        }
    }

    public static IEnumerable<(T1 item1, T2 item2, T3 item3)> Product<T1, T2, T3>(IEnumerable<T1> items1, IEnumerable<T2> items2, IEnumerable<T3> items3) {
        foreach (var item1 in items1) {
            foreach (var item2 in items2) {
                foreach (var item3 in items3) {
                    yield return (item1, item2, item3);
                }
            }
        }
    }

    public static IEnumerable<(T1 item1, T2 item2, T3 item3, T4 item4)> Product<T1, T2, T3, T4>(IEnumerable<T1> items1, IEnumerable<T2> items2, IEnumerable<T3> items3, IEnumerable<T4> items4) {
        foreach (var item1 in items1) {
            foreach (var item2 in items2) {
                foreach (var item3 in items3) {
                    foreach (var item4 in items4) {
                        yield return (item1, item2, item3, item4);
                    }
                }
            }
        }
    }
}
