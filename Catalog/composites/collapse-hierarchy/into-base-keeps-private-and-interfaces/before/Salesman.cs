using System;
using System.Collections.Generic;

namespace Staff
{
    public class Salesman : Employee, IComparable<Salesman>
    {
        private readonly List<decimal> _sales = new List<decimal>();

        public void Record(decimal amount) => _sales.Add(amount);

        public int CompareTo(Salesman other) => _sales.Count.CompareTo(other._sales.Count);
    }
}
