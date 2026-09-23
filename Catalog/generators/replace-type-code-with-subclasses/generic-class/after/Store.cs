using System;
using System.Collections.Generic;

namespace Storage
{
    public enum StoreKind
    {
        Local,
        Shared
    }

    public abstract class Store<T> where T : class
    {
        protected abstract StoreKind Kind { get; }

        public static Store<T> Create(StoreKind kind) => kind switch
        {
            StoreKind.Local => new Local<T>(),
            StoreKind.Shared => new Shared<T>(),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };

        public List<T> Items { get; } = new List<T>();

        public bool IsShared => Kind == StoreKind.Shared;
    }

    public sealed class Local<T> : Store<T> where T : class
    {
        protected override StoreKind Kind => StoreKind.Local;
    }

    public sealed class Shared<T> : Store<T> where T : class
    {
        protected override StoreKind Kind => StoreKind.Shared;
    }
}
