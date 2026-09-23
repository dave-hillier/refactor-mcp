namespace Shop
{
    public class Order
    {
        private int _price = 5;

        public int Total() => _price * 2;
    }
}
