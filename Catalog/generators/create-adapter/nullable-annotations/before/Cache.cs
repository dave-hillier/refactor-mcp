using System.Collections.Generic;

namespace Shop
{
    public interface ICache
    {
        string? Get(string key);

        void Put(string key, string? value);
    }

    public class LegacyCache
    {
        private readonly Dictionary<string, string?> _entries = new Dictionary<string, string?>();

        public string? Lookup(string key) => _entries.TryGetValue(key, out var value) ? value : null;

        public void Remember(string key, string? value) => _entries[key] = value;
    }
}
