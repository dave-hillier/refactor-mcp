using System.Collections.Generic;
using System.Linq;

namespace Shop
{
    public partial class Order
    {
        private readonly List<Line> _lines = new List<Line>();

        public IEnumerable<string> Skus() => _lines.Select(l => l.Sku).Distinct();
    }

    public class Line
    {
        public string Sku { get; set; }
    }
}
