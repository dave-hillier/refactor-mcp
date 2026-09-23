namespace Shop
{
    public class Order
    {
        public decimal Subtotal { get; set; }

        public virtual decimal Total => Subtotal;

        public string Describe() => "Total: " + Total;
    }
}
