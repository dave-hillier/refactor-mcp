using System.Collections.Generic;

namespace Storage
{
    public enum StoreKind
    {
        Local,
        Shared
    }

    public class Store<T> where T : class
    {
        private readonly StoreKind _kind;

        public Store(StoreKind kind)
        {
            _kind = kind;
        }

        public List<T> Items { get; } = new List<T>();

        public bool IsShared => _kind == StoreKind.Shared;
    }
}
