using System;

namespace Shop
{
    public static class Registry
    {
        private static DateTime _started;

        static Registry()
        {
            _started = DateTime.UtcNow;
        }

        public static TimeSpan Uptime() => DateTime.UtcNow - _started;
    }
}
