namespace Shop
{
    public class Cart : ICart
    {
        public int Count { get; private set; }

        public void Add(string item) => Count++;

        public void Add(string item, int quantity) => Count += quantity;
    }
}
