namespace Shop
{
    public class Sample
    {
        private int _count;

        public int Count { get; set; }

        public int Total() => Count + _count;
    }
}
