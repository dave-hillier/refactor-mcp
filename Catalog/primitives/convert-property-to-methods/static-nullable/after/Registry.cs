namespace Shop
{
    public static class Registry
    {
        private static string? _last;

        public static string? GetLast() => _last;

        private static void SetLast(string? value) => _last = value;

        public static void Register(string name) => SetLast(name);

        public static int Length() => GetLast()?.Length ?? 0;
    }
}
