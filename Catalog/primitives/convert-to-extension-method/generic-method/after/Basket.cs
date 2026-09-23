using System.Collections.Generic;

namespace Shop
{
    public class Basket
    {
        private readonly List<int> _quantities = new List<int>();

        public int First() => _quantities.FirstOr(0);

        public string FirstName(List<string> names) => names.FirstOr<string>("none");
    }
}
