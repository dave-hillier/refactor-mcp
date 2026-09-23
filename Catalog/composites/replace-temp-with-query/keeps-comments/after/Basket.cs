namespace Shop
{
    public class Basket
    {
        private decimal _price;
        private int _count;

        public decimal Total()
        {
            // Everything in the basket, before tax.

            // Tax is charged on the subtotal.
            return Subtotal() * 1.2m;
        }

        private decimal Subtotal()
        {
            return _price /* per item */ * _count;
        }
    }
}
