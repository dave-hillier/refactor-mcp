namespace Shop
{
    public class Customer
    {
        public Location Home { get; set; } = new Location();
    }

    public class Location
    {
        public string Street { get; set; } = "";
    }
}
