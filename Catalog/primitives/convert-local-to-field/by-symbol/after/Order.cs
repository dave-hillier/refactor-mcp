namespace Shop
{
    public class Order
    {
        private int _price = 5;
        private int _sum;

        public int Total(int quantity)
        {
            _sum = _price * quantity;
            return _sum;
        }
    }
}
