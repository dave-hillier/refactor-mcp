namespace Shop;

public class Logger
{
    public string Log(int level, string format, params object[] values)
    {
        return string.Format(format, values);
    }

    public string Run()
    {
        return Log(0, "{0} {1}", 1, 2) + Log(0, "none");
    }
}
