namespace Shop
{
    public class Session
    {
        public int TimeoutSeconds(int minutes)
        {
            return minutes * 60;
        }

        public int Minutes(int seconds) => seconds / 60;
    }
}
