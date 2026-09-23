namespace Shop
{
    public class Counter
    {
        public int Next(int count)
        {
            int /*^*/doubled = count * 2;
            count++;
            return doubled + count;
        }
    }
}
