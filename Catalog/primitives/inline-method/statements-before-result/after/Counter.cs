namespace Sample
{
    public class Counter
    {
        private int _count;

        public int Skip()
        {
            _count++;
            var first = _count;
            _count++;
            _count++;
            var third = _count;
            return first + third;
        }
    }
}
