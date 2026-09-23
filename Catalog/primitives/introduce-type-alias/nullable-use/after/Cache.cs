using System.Collections.Generic;
using Lookup = System.Collections.Generic.Dictionary<string, string>;

namespace Shop
{
    public class Cache
    {
        private Lookup? _entries;

        public Lookup Entries => _entries ??= new Lookup();
    }
}
