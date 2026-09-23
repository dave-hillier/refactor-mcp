namespace Shop
{
    public class Order
    {
        private int _price = 5;

        public int Total(int quantity)
        {
            var sum = _price * quantity;
            return sum;
        }
    }
}
