using System.Collections.Generic;

namespace Shop
{
    public partial class Order
    {
        private readonly List<Line> _lines = new List<Line>();
    }

    public class Line
    {
        public string Sku { get; set; }
    }
}
