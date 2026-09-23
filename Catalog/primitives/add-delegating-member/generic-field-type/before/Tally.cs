using System.Collections.Generic;

namespace Shop
{
    public class Tally
    {
        private readonly Dictionary<string, int> _counts = new Dictionary<string, int>();

        public void Count(string word) => _counts[word] = _counts.GetValueOrDefault(word) + 1;
    }
}
