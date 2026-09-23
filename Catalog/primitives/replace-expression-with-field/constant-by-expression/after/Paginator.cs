namespace Paging
{
    public class Paginator
    {
        private readonly int _pageSize;

        public Paginator(int pageSize)
        {
            _pageSize = pageSize;
        }

        public int Pages(int items)
        {
            return (items + _pageSize - 1) / _pageSize;
        }
    }

    public class Listing
    {
        public int Count(int items) => new Paginator(20).Pages(items);

        public int CountNamed(int items) => new Paginator(pageSize: 10 + 10).Pages(items);
    }
}
