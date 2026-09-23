namespace Shop
{
    public class Counter
    {
        private int _step = 1;

        public void Faster() => _step = 2;

        public int Next(int value) => value + _step;
    }
}
