namespace Shop
{
    public class Order
    {
        private int _quantity;

        public int Quantity
        {
            get => _quantity;
            set => _quantity = value;
        }

        public int Weight() => _quantity * 10;
    }
}
