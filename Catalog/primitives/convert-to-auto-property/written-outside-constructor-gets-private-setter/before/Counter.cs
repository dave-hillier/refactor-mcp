namespace Shop
{
    public class Counter
    {
        private int _value;

        public int Value
        {
            get { return _value; }
        }

        public void Increment() => _value++;
    }
}
