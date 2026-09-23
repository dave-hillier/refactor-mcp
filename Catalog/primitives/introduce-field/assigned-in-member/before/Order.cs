namespace Shop
{
    public class Order
    {
        private int _lines;

        public decimal Total(decimal price, int quantity)
        {
            _lines++;
            var discount = /*[*/price * quantity/*]*/ > 100 ? 5 : 0;
            return price * quantity - discount;
        }

        public int Lines => _lines;
    }
}
