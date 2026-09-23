namespace Shop
{
    public class Menu
    {
        public Offer Cheapest() => new Offer("tea", 1.5m);

        public string Label() => Cheapest().Name;
    }

    public sealed record Offer(string Name, decimal Price);
}
