namespace Shop
{
    public static class Labels
    {
        private static string _upperName;

        public static string Shout(string name)
        {
            _upperName = name.ToUpperInvariant();
            return _upperName + "!";
        }
    }
}
