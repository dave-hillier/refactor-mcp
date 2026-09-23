namespace Shop
{
    public class Cart
    {
        private int _count;

        public void Add(int quantity) => _count += quantity;

        public void Remove(int quantity) => _count -= quantity;
    }
}
