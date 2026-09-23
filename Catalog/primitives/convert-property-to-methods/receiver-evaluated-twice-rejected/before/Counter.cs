namespace Shop
{
    public class Counter
    {
        public int Value { get; set; }
    }

    public class Tally
    {
        private readonly Counter _counter = new Counter();

        public Counter Current() => _counter;

        public void Bump() => Current().Value += 1;
    }
}
