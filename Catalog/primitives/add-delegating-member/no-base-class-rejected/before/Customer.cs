namespace Shop
{
    public class Customer
    {
        public string Name { get; set; } = "";

        public string Greeting(string salutation) => salutation + " " + Name;
    }
}
