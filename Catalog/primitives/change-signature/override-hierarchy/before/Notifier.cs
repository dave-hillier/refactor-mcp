namespace Shop;

public class Notifier
{
    public virtual string Format(string message, int level)
    {
        return level + ": " + message;
    }
}

public class LoudNotifier : Notifier
{
    public override string Format(string message, int level)
    {
        return base.Format(message.ToUpperInvariant(), level) + "!";
    }
}

public class Alerts
{
    public string Raise(Notifier notifier)
    {
        return notifier.Format("disk full", 2);
    }
}
