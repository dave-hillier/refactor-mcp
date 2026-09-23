using System.Collections.Generic;

namespace Shop
{
    public class Customer
    {
        public List<string> Lines { get; } = new List<string>();

        public string Format() => string.Join(", ", Lines);
    }
}
