namespace Shop
{
    public class Address
    {
        internal string _country = "UK";

        public string Format() => "Somewhere, " + _country;
    }
}
