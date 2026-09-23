using System.Collections.Generic;

namespace Shop
{
    public class Ledger
    {
        public List<Invoice> Load() => new List<Invoice>();

        public int Count() => Load().Count;
    }
}
