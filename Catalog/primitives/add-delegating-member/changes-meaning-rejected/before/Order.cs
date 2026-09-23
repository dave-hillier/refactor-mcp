namespace Shop
{
    public class Document
    {
        public string Name { get; set; } = "";
    }

    public class Order : Document
    {
        private readonly Customer _customer = new Customer();

        public string Label() => Name + " for " + _customer.Name;
    }

    public class Archive
    {
        public string Title(Order order) => order.Name;
    }
}
