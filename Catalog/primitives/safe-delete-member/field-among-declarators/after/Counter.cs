namespace Shop
{
    public class Counter
    {
        private int _count, _limit = 10;

        public bool Next() => ++_count < _limit;
    }
}
