using System;

namespace Energy
{
    public class Tariff
    {
        public int Charge(int usage)
        {
            return MiddleBand(usage) * 5 + TopBand(usage) * 7;
        }

        private int MiddleBand(int usage)
        {
            return Math.Max(0, Math.Min(usage, 200) - 100);
        }

        private int TopBand(int usage)
        {
            return Math.Max(0, Math.Min(usage, 1000) - 200);
        }
    }
}
