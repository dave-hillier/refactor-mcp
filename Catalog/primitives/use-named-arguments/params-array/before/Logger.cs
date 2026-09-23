namespace Shop;

public class Logger
{
    public string Log(string format, params object[] values)
    {
        return string.Format(format, values);
    }

    public string Run()
    {
        return /*^*/Log("{0} {1}", 1, 2);
    }
}
