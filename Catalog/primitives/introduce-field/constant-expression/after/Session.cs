namespace Shop
{
    public class Session
    {
        private readonly int _secondsPerMinute = 60;

        public int TimeoutSeconds(int minutes)
        {
            return minutes * _secondsPerMinute;
        }
    }
}
