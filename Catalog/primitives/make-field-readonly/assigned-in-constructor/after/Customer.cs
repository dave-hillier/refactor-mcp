namespace Shop
{
    public class Customer
    {
        private readonly string _name;

        public Customer(string name)
        {
            _name = name;
        }

        public string Greeting() => "Dear " + _name;
    }
}
