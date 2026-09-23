namespace Shop
{
    public class Order
    {
        private int _lines;
        private decimal _subtotal;

        public decimal Total(decimal price, int quantity)
        {
            _lines++;
            _subtotal = price * quantity;
            var discount = _subtotal > 100 ? 5 : 0;
            return price * quantity - discount;
        }

        public int Lines => _lines;
    }
}
