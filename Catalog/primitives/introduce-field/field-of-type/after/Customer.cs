namespace Shop
{
    public class Customer
    {
        public string Name = "";
        private readonly Address _address = new Address();

        public string Greeting() => "Dear " + Name;
    }
}
