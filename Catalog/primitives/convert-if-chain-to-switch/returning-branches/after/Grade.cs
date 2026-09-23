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
            switch (level)
            {
                case Level.Low:
                    return "low";
                case Level.Medium:
                    return "medium";
                default:
                    return "high";
            }
        }
    }
}
