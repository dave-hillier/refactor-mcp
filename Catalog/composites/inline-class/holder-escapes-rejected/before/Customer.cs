using System;

namespace Shop
{
    public class Customer
    {
        private readonly Address _address = new Address();

        public void Print()
        {
            Console.WriteLine(_address);
        }
    }
}
