namespace Shop;

public class Notifier
{
    public virtual string Format(string message, int level)
    {
        return message;
    }
}

public class LoudNotifier : Notifier
{
    public override string Format(string message, int level)
    {
        return message + new string('!', level);
    }
}
