namespace Shop
{
    internal class Part
    {
    }

    internal class Engine
    {
        public Part Spare() => new Part();
    }

    public class Car
    {
        private readonly Engine _engine = new Engine();

        public bool HasEngine() => _engine != null;
    }
}
