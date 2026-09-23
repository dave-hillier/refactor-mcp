namespace Shop
{
    public class Settings
    {
        public static int Retries = 3;

        private int _timeout;

        public static int Timeout() => Retries * 10;
    }
}
