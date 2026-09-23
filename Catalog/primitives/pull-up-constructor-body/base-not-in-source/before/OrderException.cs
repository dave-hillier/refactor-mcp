using System;

namespace Shop
{
    public class OrderException : Exception
    {
        public int OrderId;

        public OrderException(int orderId)
        {
            OrderId = orderId;
        }
    }
}
