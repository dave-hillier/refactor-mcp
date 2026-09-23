namespace Shop
{
    public class Session
    {
        private const int SecondsPerMinute = 60;

        public int TimeoutSeconds(int minutes)
        {
            return minutes * SecondsPerMinute;
        }
    }
}
