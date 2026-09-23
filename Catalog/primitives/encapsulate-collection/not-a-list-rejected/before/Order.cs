using System.Collections.Generic;

namespace Shop
{
    public class Order
    {
        private readonly HashSet<string> _codes = new HashSet<string>();

        public HashSet<string> Codes => _codes;
    }
}
