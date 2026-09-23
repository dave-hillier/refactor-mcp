namespace Shop
{
    public class Order
    {
        public string Number { get; set; }

        public static Order Parse(string text) => new Order { Number = text };
    }
}
