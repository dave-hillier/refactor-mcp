namespace Shop
{
    public static class Registry
    {
        private static int _instances;

        public static int Instances
        {
            get => _instances;
            set => _instances = value;
        }

        public static void Register() => Instances++;
    }
}
