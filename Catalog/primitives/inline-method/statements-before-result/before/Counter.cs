namespace Sample
{
    public class Counter
    {
        private int _count;

        public int Skip()
        {
            var first = Next();
            Next();
            var third = Next();
            return first + third;
        }

        private int Next()
        {
            _count++;
            return _count;
        }
    }
}
