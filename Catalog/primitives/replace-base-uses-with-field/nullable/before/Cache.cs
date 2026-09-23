#nullable enable

using System.Collections.Generic;

namespace Shop
{
    public class Cache : Dictionary<string, string?>
    {
        private readonly Dictionary<string, string?> _entries = new Dictionary<string, string?>();

        public string? Lookup(string key) => TryGetValue(key, out var value) ? value : null;
    }
}
