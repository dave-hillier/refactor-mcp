namespace Shop
{
    public partial class Order
    {
        public int Count => _lines.Count;
    }
}
