namespace Heating
{
    public class Room
    {
        public Room(TempRange daysTempRange)
        {
            DaysTempRange = daysTempRange;
        }

        public TempRange DaysTempRange { get; }

        public bool IsComfortable(HeatingPlan plan)
        {
            return plan.WithinRange(DaysTempRange);
        }
    }
}
