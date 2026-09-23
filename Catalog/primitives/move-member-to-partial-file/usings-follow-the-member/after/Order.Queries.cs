using System.Collections.Generic;
using System.Linq;

namespace Shop
{
    public partial class Order
    {
        public int Count => _lines.Count;

        public IEnumerable<string> Skus() => _lines.Select(l => l.Sku).Distinct();
    }
}
