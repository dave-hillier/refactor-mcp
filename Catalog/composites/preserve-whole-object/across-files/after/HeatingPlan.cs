namespace Heating
{
    public class TempRange
    {
        public TempRange(int low, int high)
        {
            Low = low;
            High = high;
        }

        public int Low { get; }

        public int High { get; }
    }

    public class HeatingPlan
    {
        private readonly TempRange _allowed;

        public HeatingPlan(TempRange allowed)
        {
            _allowed = allowed;
        }

        public bool WithinRange(TempRange range)
        {
            return range.Low >= _allowed.Low && range.High <= _allowed.High;
        }
    }
}
