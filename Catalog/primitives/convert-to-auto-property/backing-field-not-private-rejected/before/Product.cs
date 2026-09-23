namespace Shop
{
    public class Product
    {
        protected decimal _price;

        public decimal Price
        {
            get => _price;
            set => _price = value;
        }
    }

    public class Book : Product
    {
        public void Discount() => _price *= 0.9m;
    }
}
