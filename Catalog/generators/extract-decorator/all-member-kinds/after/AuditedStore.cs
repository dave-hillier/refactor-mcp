using System;
using System.Collections.Generic;

namespace Shop
{
    public class AuditedStore : IStore
    {
        private readonly IStore _inner;

        public AuditedStore(IStore inner)
        {
            _inner = inner;
        }

        public event EventHandler Changed
        {
            add => _inner.Changed += value;
            remove => _inner.Changed -= value;
        }

        public string Name
        {
            get => _inner.Name;
            set => _inner.Name = value;
        }

        public int Count => _inner.Count;

        public string this[int index]
        {
            get => _inner[index];
            set => _inner[index] = value;
        }

        public void Clear() => _inner.Clear();

        public bool TryGet(string key, out string value) => _inner.TryGet(key, out value);

        public void Swap(ref int first, ref int second) => _inner.Swap(ref first, ref second);

        public int Sum(params int[] values) => _inner.Sum(values);

        public void Add(string key, string value = "none") => _inner.Add(key, value);

        public IEnumerable<string> Keys() => _inner.Keys();
    }
}
