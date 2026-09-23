namespace Shop
{
    public class Order
    {
        private int _quantity = 1;

        public int GetQuantity() => _quantity;

        public void SetQuantity(int value) => _quantity = value;

        public bool IsBulk() => GetQuantity() > 10;
    }
}
