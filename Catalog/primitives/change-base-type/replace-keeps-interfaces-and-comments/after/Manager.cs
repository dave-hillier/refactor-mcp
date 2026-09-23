using System;

namespace Shop
{
    // Managers approve orders.
    public class Manager : Employee, IComparable<Manager> // ordered by name
    {
        public int CompareTo(Manager other) => string.CompareOrdinal(Name, other.Name);
    }
}
