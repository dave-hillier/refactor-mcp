namespace Shop
{
    public class Sample
    {
        public int Count { get; set; }

        public int GetCount(int extra) => Count + extra;
    }
}
