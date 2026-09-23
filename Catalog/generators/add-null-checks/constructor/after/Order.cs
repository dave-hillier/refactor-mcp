using System;

namespace Shop
{
    public class Customer
    {
    }

    public class Entity
    {
        protected Entity(string id)
        {
            Id = id;
        }

        public string Id { get; }
    }

    public class Order : Entity
    {
        private readonly Customer _customer;

        public Order(Customer customer, string id) : base(id)
        {
            ArgumentNullException.ThrowIfNull(customer);
            ArgumentNullException.ThrowIfNull(id);

            _customer = customer;
        }
    }
}
