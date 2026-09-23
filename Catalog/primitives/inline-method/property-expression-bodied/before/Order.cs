namespace Shop
{
    public class Order
    {
        public decimal Subtotal { get; set; }

        public decimal Tax { get; set; }

        public decimal Total => Subtotal + Tax;

        public string Describe() => "Total: " + Total;
    }
}
