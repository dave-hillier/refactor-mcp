namespace Shop
{
    public class Customer
    {
        private readonly Address _address = new Address();
        // Where the customer lives.
        public string city = "London";

        public string Name { get; set; }

        public void MoveTo(string city)
        {
            this.city = city;
        }

        public string Label() => Name + ", " + this.city;
    }
}
