namespace Shop
{
    public class Order
    {
        public int Id { get; }

        public string Label() => "#" + Id;
    }
}
