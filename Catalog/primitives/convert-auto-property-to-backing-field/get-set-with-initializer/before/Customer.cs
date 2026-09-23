namespace Shop
{
    public class Customer
    {
        public string Name { get; set; } = "unknown";

        public string Greeting() => "Dear " + Name;
    }
}
