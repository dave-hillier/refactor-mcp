namespace Shop;

public interface IClock
{
    string Stamp(int hour, string pattern = "t");
}

public class FixedClock : IClock
{
    public string Stamp(int hour, string pattern = "t")
    {
        return hour + pattern;
    }
}

public class Log
{
    public string Write(IClock clock, FixedClock fixedClock)
    {
        return clock.Stamp(9) + fixedClock.Stamp(10);
    }
}
