namespace Shop
{
    public class Order
    {
        private decimal _total;

        public void Add(decimal price) => _total += price;
    }
}
