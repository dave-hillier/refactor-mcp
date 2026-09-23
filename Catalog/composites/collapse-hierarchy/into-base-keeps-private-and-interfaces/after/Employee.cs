using System;
using System.Collections.Generic;

namespace Staff
{
    public class Employee : IComparable<Employee>
    {
        private readonly List<decimal> _sales = new List<decimal>();

        public string Name { get; set; }

        public void Record(decimal amount) => _sales.Add(amount);

        public int CompareTo(Employee other) => _sales.Count.CompareTo(other._sales.Count);
    }
}
