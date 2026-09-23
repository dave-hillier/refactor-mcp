using Shop.Billing;

namespace Shop
{
    public class Checkout
    {
        public Invoice Bill(decimal net) => new Invoice { Net = net };
    }
}
