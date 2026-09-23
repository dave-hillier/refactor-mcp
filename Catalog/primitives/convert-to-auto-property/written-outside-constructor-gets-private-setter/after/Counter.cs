namespace Shop
{
    public class Counter
    {
        public int Value { get; private set; }

        public void Increment() => Value++;
    }
}
