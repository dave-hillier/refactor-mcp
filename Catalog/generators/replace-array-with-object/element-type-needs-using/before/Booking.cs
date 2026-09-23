using System;

namespace Shop
{
    public class Booking
    {
        private readonly DateTime[] _stay;

        public Booking(DateTime arrival, int nights)
        {
            _stay = new DateTime[] { arrival, arrival.AddDays(nights) };
        }

        public TimeSpan Length => _stay[1] - _stay[0];
    }
}
