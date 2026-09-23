namespace Shop
{
    public class Clock
    {
        private int _offset;
        private const int SecondsPerMinute = 60;

        public int ToSeconds(int minutes)
        {
            return minutes * SecondsPerMinute + _offset;
        }

        public double ToMinutes(int seconds)
        {
            return seconds / SecondsPerMinute + seconds % SecondsPerMinute / 60.0;
        }

        public string Label() => "60 per minute, 600 per ten";
    }
}
