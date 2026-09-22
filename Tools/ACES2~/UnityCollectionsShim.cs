// Minimal stand-in for Unity.Collections so the generated port compiles under plain .NET for validation.
namespace Unity.Collections
{
    public enum Allocator { Invalid, None, Temp, TempJob, Persistent }
    public enum NativeArrayOptions { ClearMemory, UninitializedMemory }
    public struct NativeArray<T> where T : struct
    {
        readonly T[] _items;
        public NativeArray(int length, Allocator allocator, NativeArrayOptions options = NativeArrayOptions.ClearMemory) { _items = new T[length]; }
        public T this[int index] { get => _items[index]; set => _items[index] = value; }
        public int Length => _items.Length;
        public void CopyFrom(NativeArray<T> source) => System.Array.Copy(source._items, _items, _items.Length);
        public void Dispose() { }
    }
}
