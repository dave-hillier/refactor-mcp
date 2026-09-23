using System;
using System.Collections.Generic;

namespace Shop
{
    public interface IStore
    {
        event EventHandler Changed;

        string Name { get; set; }

        int Count { get; }

        string this[int index] { get; set; }

        void Clear();

        bool TryGet(string key, out string value);

        void Swap(ref int first, ref int second);

        int Sum(params int[] values);

        void Add(string key, string value = "none");

        IEnumerable<string> Keys();
    }
}
