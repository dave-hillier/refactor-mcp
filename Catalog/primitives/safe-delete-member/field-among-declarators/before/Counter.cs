namespace Shop
{
    public class Counter
    {
        private int _count, _spare, _limit = 10;

        public bool Next() => ++_count < _limit;
    }
}
