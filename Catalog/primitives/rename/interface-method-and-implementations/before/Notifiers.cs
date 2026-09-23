namespace Shop
{
    public class EmailNotifier : INotifier
    {
        public void Send(string message)
        {
        }
    }

    public class SmsNotifier : INotifier
    {
        void INotifier.Send(string message)
        {
        }
    }

    public class Alerts
    {
        public void Raise(INotifier notifier, EmailNotifier email)
        {
            notifier.Send("down");
            email.Send("down");
        }
    }
}
