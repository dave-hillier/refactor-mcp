using System;

namespace Shop
{
    public class DirectoryDecorator : IDirectory
    {
        private readonly IDirectory _inner;

        public DirectoryDecorator(IDirectory inner)
        {
            _inner = inner;
        }

        public event EventHandler? Changed
        {
            add => _inner.Changed += value;
            remove => _inner.Changed -= value;
        }

        public string? Find(string key) => _inner.Find(key);

        public void Add(string key, string? value) => _inner.Add(key, value);
    }
}
