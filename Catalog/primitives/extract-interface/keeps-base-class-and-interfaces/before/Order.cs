using System;

namespace Shop
{
    public abstract class Entity
    {
        public int Id;
    }

    public class Order : Entity, IComparable<Order>
    {
        public int CompareTo(Order other) => Id.CompareTo(other.Id);

        public string Describe() => "Order " + Id;
    }
}
