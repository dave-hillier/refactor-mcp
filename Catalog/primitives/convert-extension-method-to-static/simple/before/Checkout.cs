namespace Shop
{
    public class Checkout
    {
        public string Welcome(string name) => name.Shout();

        public string Static(string name) => Text.Shout(name);
    }
}
