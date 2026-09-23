namespace Shop
{
    public static class Schedule
    {
        // Days are counted from midnight.
        public static int MinutesUntil(int day)
        {
            // Whole days first.
            return day * /*[*/(24 * 60)/*]*/; // no daylight saving
        }
    }
}
