namespace Shop
{
    public class Counter
    {
        private readonly int _step;

        public Counter(int step)
        {
            _step = step;
        }

        public int Next(int value) => value + _step;
    }
}
