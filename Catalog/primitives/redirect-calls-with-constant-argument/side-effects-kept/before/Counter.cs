namespace Tally
{
    public class Counter
    {
        private int _total;

        public void Apply(string mode, int amount)
        {
            if (mode == "reset")
                Reset();
            else if (mode == "add")
                Add(amount);
        }

        public void Reset()
        {
            _total = 0;
        }

        public void Add(int amount)
        {
            _total += amount;
        }
    }

    public class Client
    {
        private int _calls;

        public void Run(Counter counter, int start)
        {
            counter.Apply("reset", start);
            counter.Apply("reset", Next());
            counter.Apply("add", Next());
        }

        private int Next() => ++_calls;
    }
}
