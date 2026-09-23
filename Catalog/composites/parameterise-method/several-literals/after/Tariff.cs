using System;

namespace Energy
{
    public class Tariff
    {
        public int Charge(int usage)
        {
            return WithinBand(usage, 200, 100) * 5 + WithinBand(usage, 1000, 200) * 7;
        }

        private int WithinBand(int usage, int top, int bottom)
        {
            return Math.Max(0, Math.Min(usage, top) - bottom);
        }
    }
}
