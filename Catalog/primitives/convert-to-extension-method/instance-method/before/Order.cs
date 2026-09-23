namespace Shop
{
    public class Order
    {
        public string Number { get; set; }

        public decimal Total { get; set; }

        public string Describe()
        {
            return Number + ": " + Total;
        }
    }
}
