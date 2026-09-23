namespace Shop
{
    public class Customer
    {
        private string _name = "unknown";

        public string Name
        {
            get => _name;
            set => _name = value;
        }

        public string Greeting() => "Dear " + Name;
    }
}
