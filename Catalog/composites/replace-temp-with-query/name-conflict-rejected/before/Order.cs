namespace Shop
{
    public class Order
    {
        private readonly int[] _lines = { 1, 2, 3 };

        public int Quantity { get; set; }

        public int Total()
        {
            int /*^*/count = _lines.Length;
            return count * 10;
        }
    }
}
