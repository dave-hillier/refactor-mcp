namespace Shop
{
    public class Order
    {
        public decimal Total { get; set; }

        public string Format() => Total.ToString();

        public string Report() => "Total: " + Format();
    }
}
