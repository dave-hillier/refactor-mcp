using System;

namespace Shop
{
    public class Counter
    {
        public int GetCount() => 3;

        public Func<int> Reader() => GetCount;
    }
}
