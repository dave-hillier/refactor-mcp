namespace Shop
{
    public static class Registry
    {
        public static string? Last { get; private set; }

        public static void Register(string name) => Last = name;

        public static int Length() => Last?.Length ?? 0;
    }
}
