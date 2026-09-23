namespace Shop
{
    public static class Registry
    {
        public static int Instances { get; set; }

        public static void Register() => Instances++;
    }
}
