namespace Shop
{
    public class Basket
    {
        public int Remaining(int count) => Limits.MaxItems - count;

        public int Share(int total) => total / Limits.MaxItems;
    }
}
