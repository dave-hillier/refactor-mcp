namespace Shop
{
    public class Settings
    {
        public int Retries;

        /// <summary>Settings with every value at its default.</summary>
        public static Settings Default => new Settings { Retries = 3 };
    }

    public class Client
    {
        public int Retries() => Settings.Default.Retries;
    }
}
