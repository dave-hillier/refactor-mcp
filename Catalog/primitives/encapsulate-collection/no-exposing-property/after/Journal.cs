using System.Collections.Generic;

namespace Shop
{
    public class Journal
    {
        private readonly List<decimal> _entries = new List<decimal>();

        public IReadOnlyList<decimal> Entries => _entries.AsReadOnly();

        public void AddEntry(decimal entry) => _entries.Add(entry);

        public bool RemoveEntry(decimal entry) => _entries.Remove(entry);

        public decimal Balance()
        {
            var total = 0m;
            foreach (var entry in _entries)
                total += entry;
            return total;
        }
    }
}
