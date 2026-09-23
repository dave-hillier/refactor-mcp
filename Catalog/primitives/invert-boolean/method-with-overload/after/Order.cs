using System;

namespace Shop
{
    public class Order
    {
        public void Place(Validator validator, int quantity, string code)
        {
            if (validator.IsInvalid(quantity) || !validator.IsValid(code))
            {
                throw new ArgumentException("invalid order");
            }

            var ok = !validator.IsInvalid(quantity + 1);
            Console.WriteLine(ok);
        }
    }
}
