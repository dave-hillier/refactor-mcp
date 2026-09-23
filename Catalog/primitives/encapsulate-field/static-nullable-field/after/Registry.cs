namespace Shop
{
    public static class Registry
    {
        private static string? _lastName;

        public static string? LastName
        {
            get => _lastName;
            set => _lastName = value;
        }
    }

    public class Customer
    {
        public void Register(string name) => Registry.LastName = name;
    }
}
