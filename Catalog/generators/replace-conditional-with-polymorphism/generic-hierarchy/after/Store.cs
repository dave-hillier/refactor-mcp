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

        public virtual string Describe(T item) => "unknown";
    }

    public sealed class Local<T> : Store<T>
    {
        protected override StoreKind Kind => StoreKind.Local;

        public override string Describe(T item) => "local " + item;
    }

    public sealed class Shared<TValue> : Store<TValue>
    {
        protected override StoreKind Kind => StoreKind.Shared;

        public override string Describe(TValue item)
        {
            var copies = new List<TValue> { item, item };
            return "shared " + copies.Count;
        }
    }

    public sealed class Mirrored<TValue> : Store<TValue>
    {
        protected override StoreKind Kind => StoreKind.Mirrored;

        public override string Describe(TValue item)
        {
            var copies = new List<TValue> { item, item };
            return "shared " + copies.Count;
        }
    }
}
