namespace Shop
{
    public static class Schedule
    {
        private const int MinutesPerDay = 24 * 60;

        // Days are counted from midnight.
        public static int MinutesUntil(int day)
        {
            // Whole days first.
            return day * MinutesPerDay; // no daylight saving
        }
    }
}
