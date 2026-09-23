using System.Collections.Generic;

namespace Shop
{
    public class Ledger
    {
        public List<T> Load<T>() => new List<T>();

        public int Count() => Load<Invoice>().Count;
    }
}
