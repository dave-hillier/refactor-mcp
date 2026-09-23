namespace Shop
{
    public class Counter
    {
        private int _value;

        public void Swap(ref int other)
        {
            var old = _value;
            _value = other;
            other = old;
        }
    }
}
