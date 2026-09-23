using System.Collections.Generic;

namespace Shop
{
    public class Basket
    {
        private readonly List<int> _quantities = new List<int>();

        public int First() => Sequences.FirstOr(_quantities, 0);

        public string FirstName(List<string> names) => Sequences.FirstOr<string>(names, "none");
    }
}
