namespace Heating
{
    public record TempRange(int Low, int High);

    public class Room
    {
        public TempRange DaysTempRange { get; set; } = new TempRange(18, 21);
    }

    public class HeatingPlan
    {
        public string Describe(string label, int low, int high)
        {
            return label + ": " + low + " to " + high + " (" + (high - low) + " degrees)";
        }

        public string Report(Room room, TempRange forecast)
        {
            var today = Describe("today", room.DaysTempRange.Low, room.DaysTempRange.High);
            return today + "; " + Describe("forecast", forecast.Low, forecast.High);
        }
    }
}
