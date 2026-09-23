#nullable enable

using System.Collections.Generic;

namespace Shop
{
    public class Directory
    {
        private readonly Dictionary<string, string> _emails = new Dictionary<string, string>();
        private string? _lastLookup;

        public string? LastLookup => _lastLookup;

        public string? Find(string name)
        {
            _lastLookup = name;
            if (_emails.TryGetValue(name, out var email))
                return email;

            return null;
        }

        public string Describe(string name, string? fallback = null)
        {
            var email = Find(name);
            return email ?? fallback ?? "unknown";
        }
    }
}
