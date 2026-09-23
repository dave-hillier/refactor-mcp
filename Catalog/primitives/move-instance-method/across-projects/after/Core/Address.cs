namespace Core
{
    public class Address
    {
        public string City { get; set; }

        public string Town() => City.ToUpperInvariant();
    }
}
