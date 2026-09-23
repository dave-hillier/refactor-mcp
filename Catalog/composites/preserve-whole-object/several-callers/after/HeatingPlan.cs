namespace Heating
{
    public record TempRange(int Low, int High);

    public class Room
    {
        public TempRange DaysTempRange { get; set; } = new TempRange(18, 21);
    }

    public class HeatingPlan
    {
        public string Describe(string label, TempRange range)
        {
            return label + ": " + range.Low + " to " + range.High + " (" + (range.High - range.Low) + " degrees)";
        }

        public string Report(Room room, TempRange forecast)
        {
            var today = Describe("today", room.DaysTempRange);
            return today + "; " + Describe("forecast", forecast);
        }
    }
}
