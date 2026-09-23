namespace Shop;

public class Notifier
{
    protected virtual string Format(string message)
    {
        return message;
    }
}

public class LoudNotifier : Notifier
{
    protected override string Format(string message)
    {
        return message + "!";
    }
}
