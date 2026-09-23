namespace Shop
{
    public static class Program
    {
        public static void Run() => new Report().Print(new FileWriter());
    }
}
