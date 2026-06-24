using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace RvB.Collections;

#if NET10_0_OR_GREATER
public static class ArrayExtensions {
    extension<T>(T[] array) {
        public void ForEach(Action<T> action) {
            foreach (var value in array) {
                action(value);
            }
        }

        public T[] Update(Func<T,T> transform) {
            for (var i = 0; i < array.Length; i++) {
                array[i] = transform(array[i]);
            }
            return array;
        }
    }

    extension<T>(T[,] array) {
        public void Fill(T value) {
            ref byte reference = ref MemoryMarshal.GetArrayDataReference(array);
            Span<T> span = MemoryMarshal.CreateSpan(ref Unsafe.As<byte, T>(ref reference), array.Length);
            span.Fill(value);
        }
    }
}
#endif
