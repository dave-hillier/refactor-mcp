namespace Shop
{
    public static class Registry
    {
        public static string? LastName;
    }

    public class Customer
    {
        public void Register(string name) => Registry.LastName = name;
    }
}
