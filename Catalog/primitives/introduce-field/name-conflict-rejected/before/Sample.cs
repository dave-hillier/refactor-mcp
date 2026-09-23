namespace Shop
{
    public class Sample
    {
        private int _total;

        public int Run(int x)
        {
            _total += x;
            return /*[*/x * 2/*]*/ + _total;
        }
    }
}
