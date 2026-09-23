using System;

namespace Shop
{
    public class Booking
    {
        private readonly Stay _stay;

        public Booking(DateTime arrival, int nights)
        {
            _stay = new Stay { Arrival = arrival, Departure = arrival.AddDays(nights) };
        }

        public TimeSpan Length => _stay.Departure - _stay.Arrival;
    }
}
