using System.Collections.Generic;
using StockIndex = System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<int>>;

namespace Shop
{
    public class Inventory
    {
        private readonly StockIndex _stock = new StockIndex();

        public StockIndex Snapshot() => new StockIndex(_stock);

        public List<int> Levels(string sku) => _stock[sku];

        public Dictionary<string, int> Totals() => new Dictionary<string, int>();
    }
}
