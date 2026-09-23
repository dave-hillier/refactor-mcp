using System.Collections.Generic;

namespace Shop
{
    public class Cache
    {
        private Dictionary<string, string>? _entries;

        public Dictionary<string, string> Entries => _entries ??= new Dictionary<string, string>();
    }
}
