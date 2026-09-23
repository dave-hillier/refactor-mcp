namespace Shop
{
    public enum Level
    {
        Low,
        Medium,
        High,
    }

    public class Grade
    {
        public string Describe(Level level)
        {
            /*^*/if (level == Level.Low)
                return "low";
            else if (Level.Medium == level)
                return "medium";
            else
                return "high";
        }
    }
}
