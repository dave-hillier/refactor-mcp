namespace Shop
{
    public class Basket
    {
        public int Remaining(int count) => Limits.Base * 2 - count;

        public int Share(int total) => total / (Limits.Base * 2);
    }
}
