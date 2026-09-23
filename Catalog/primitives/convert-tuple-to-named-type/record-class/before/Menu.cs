namespace Shop
{
    public class Menu
    {
        public (string Name, decimal Price) Cheapest() => ("tea", 1.5m);

        public string Label() => Cheapest().Name;
    }
}
