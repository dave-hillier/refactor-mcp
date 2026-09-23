namespace Shop
{
    public class Party
    {
        public string Name { get; set; }
    }

    public class Customer : Party
    {
        private readonly Address _address = new Address();

        public string Greeting() => "Dear " + Name + " of " + _address.Town;
    }

    public class Address
    {
        public string Town { get; set; }
    }
}
