using System.Collections.Generic;

namespace Storage
{
    public enum StoreKind
    {
        Local,
        Shared,
        Mirrored
    }

    public abstract class Store<T>
    {
        protected abstract StoreKind Kind { get; }

        public string Describe(T item)
        {
            switch (Kind)
            {
                case StoreKind.Local:
                    return "local " + item;
                case StoreKind.Shared:
                case StoreKind.Mirrored:
                    var copies = new List<T> { item, item };
                    return "shared " + copies.Count;
                default:
                    return "unknown";
            }
        }
    }

    public sealed class Local<T> : Store<T>
    {
        protected override StoreKind Kind => StoreKind.Local;
    }

    public sealed class Shared<TValue> : Store<TValue>
    {
        protected override StoreKind Kind => StoreKind.Shared;
    }

    public sealed class Mirrored<TValue> : Store<TValue>
    {
        protected override StoreKind Kind => StoreKind.Mirrored;
    }
}
