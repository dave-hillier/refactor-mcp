namespace Shop
{
    public class Counter
    {
        private int _value;

        public int Value => _value;

        public void Update(int value)
        {
            _value = value;
        }
    }
}
