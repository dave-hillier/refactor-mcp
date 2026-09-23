namespace Shop
{
    public class Address
    {
        public string Town { get; set; }

        public string Label(string prefix) => prefix + Town;
    }
}
