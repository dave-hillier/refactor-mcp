namespace Shop
{
    public static class Limits
    {
        public const int Base = 5;

        public static bool IsFull(int count) => count >= Base * 2;
    }
}
