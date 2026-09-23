namespace Shop
{
    public static class Limits
    {
        public const int Base = 5;
        public const int MaxItems = Base * 2;

        public static bool IsFull(int count) => count >= MaxItems;
    }
}
