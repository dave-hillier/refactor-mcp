using System;

namespace Shop
{
    public class OrderService
    {
        public string Stamp(string order)
        {
            var /*^*/now = DateTime.Now;
            return order + now.Ticks;
        }
    }
}
