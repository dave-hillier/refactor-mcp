using System.Collections.Generic;

namespace Shop
{
    public class Address
    {
        public List<string> Lines { get; } = new List<string>();

        public string Format() => string.Join(", ", Lines);
    }
}
