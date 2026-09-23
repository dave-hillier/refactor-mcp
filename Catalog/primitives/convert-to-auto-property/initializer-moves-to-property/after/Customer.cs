namespace Shop
{
    public class Customer
    {
        private int _visits;

        public string Name { get; set; } = "unknown";

        public int Visits => _visits;
    }
}
