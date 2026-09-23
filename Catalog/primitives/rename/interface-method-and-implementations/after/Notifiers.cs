namespace Shop
{
    public class EmailNotifier : INotifier
    {
        public void Notify(string message)
        {
        }
    }

    public class SmsNotifier : INotifier
    {
        void INotifier.Notify(string message)
        {
        }
    }

    public class Alerts
    {
        public void Raise(INotifier notifier, EmailNotifier email)
        {
            notifier.Notify("down");
            email.Notify("down");
        }
    }
}
