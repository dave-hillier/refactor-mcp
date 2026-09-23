namespace Shop
{
    public interface ICounter
    {
        int Count();
    }

    public class Basket
    {
        private ICounter _counter;

        public long Total() => _counter?.Count() ?? 0L;
    }
}
