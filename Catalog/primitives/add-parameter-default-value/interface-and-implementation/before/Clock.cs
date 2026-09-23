namespace Shop;

public interface IClock
{
    string Stamp(int hour, string pattern);
}

public class FixedClock : IClock
{
    public string Stamp(int hour, string pattern)
    {
        return hour + pattern;
    }
}

public class Log
{
    public string Write(IClock clock, FixedClock fixedClock)
    {
        return clock.Stamp(9, "t") + fixedClock.Stamp(10, "t");
    }
}
