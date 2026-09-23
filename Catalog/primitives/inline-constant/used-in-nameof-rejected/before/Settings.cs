namespace Shop
{
    public class Settings
    {
        public const int Retries = 3;

        public string Describe() => nameof(Retries) + " = " + Retries;
    }
}
