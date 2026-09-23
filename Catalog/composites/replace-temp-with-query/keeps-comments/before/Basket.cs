namespace Shop
{
    public class Basket
    {
        private decimal _price;
        private int _count;

        public decimal Total()
        {
            // Everything in the basket, before tax.
            decimal /*^*/subtotal = _price /* per item */ * _count;

            // Tax is charged on the subtotal.
            return subtotal * 1.2m;
        }
    }
}
