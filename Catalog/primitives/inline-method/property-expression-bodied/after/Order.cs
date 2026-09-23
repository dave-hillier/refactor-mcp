namespace Shop
{
    public class Order
    {
        public decimal Subtotal { get; set; }

        public decimal Tax { get; set; }

        public string Describe() => "Total: " + (Subtotal + Tax);
    }
}
