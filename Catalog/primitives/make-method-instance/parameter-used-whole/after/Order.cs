using System.Collections.Generic;

namespace Shop
{
    public class Order
    {
        public string Number { get; set; }

        public void Register(List<Order> registry)
        {
            var Number = registry.Count.ToString();
            this.Number = Number;
            registry.Add(this);
        }
    }
}
