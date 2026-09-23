namespace Shop
{
    public class Counter
    {
        private int _value;

        public int Next()
        {
            return ++_value;
        }
    }
}
