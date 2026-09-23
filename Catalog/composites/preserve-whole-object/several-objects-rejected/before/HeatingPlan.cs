namespace Heating
{
    public record TempRange(int Low, int High);

    public class HeatingPlan
    {
        public bool WithinRange(int low, int high)
        {
            return low >= 16 && high <= 24;
        }

        public bool Check(TempRange morning, TempRange evening)
        {
            return WithinRange(morning.Low, evening.High);
        }
    }
}
