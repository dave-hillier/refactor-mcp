namespace Shop
{
    public class Clock
    {
        private int _offset;

        public int ToSeconds(int minutes)
        {
            return minutes * /*[*/60/*]*/ + _offset;
        }

        public double ToMinutes(int seconds)
        {
            return seconds / 60 + seconds % 60 / 60.0;
        }

        public string Label() => "60 per minute, 600 per ten";
    }
}
