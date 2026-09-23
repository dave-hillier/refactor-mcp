namespace Heating
{
    public class HeatingPlan
    {
        public bool WithinRange(int low, int high)
        {
            return low >= 16 && high <= 24;
        }
    }
}
