using System.Collections.Generic;

namespace Shop
{
    public class Inventory
    {
        private readonly Dictionary<string, List<int>> _stock = new Dictionary<string, List<int>>();

        public Dictionary<string, List<int>> Snapshot() => new Dictionary<string, List<int>>(_stock);

        public List<int> Levels(string sku) => _stock[sku];
    }
}
