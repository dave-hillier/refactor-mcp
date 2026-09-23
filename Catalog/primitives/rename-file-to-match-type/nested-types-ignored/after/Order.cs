using System.Collections.Generic;

namespace Shop
{
    public class Order
    {
        private readonly List<Line> _lines = new List<Line>();

        public void Add(string sku) => _lines.Add(new Line { Sku = sku });

        private class Line
        {
            public string Sku;
        }
    }
}
