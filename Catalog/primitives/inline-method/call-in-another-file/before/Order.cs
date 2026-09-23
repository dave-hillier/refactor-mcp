namespace Shop
{
    public class Order
    {
        public string Name { get; set; } = "";

        public int Quantity { get; set; }

        public string Describe() => $"{Name} x {Quantity}";
    }
}
