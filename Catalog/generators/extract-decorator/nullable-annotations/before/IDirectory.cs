using System;

namespace Shop
{
    public interface IDirectory
    {
        event EventHandler? Changed;

        string? Find(string key);

        void Add(string key, string? value);
    }
}
