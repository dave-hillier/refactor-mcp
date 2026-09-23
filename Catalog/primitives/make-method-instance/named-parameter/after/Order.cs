namespace Shop
{
    public class Order
    {
        public string Number { get; set; }

        public string Label(string prefix) => prefix + Number;
    }
}
