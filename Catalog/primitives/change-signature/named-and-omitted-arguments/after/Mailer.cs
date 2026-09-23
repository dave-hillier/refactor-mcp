namespace Shop;

public class Mailer
{
    public string Compose(string subject, string to, int priority = 0, bool urgent = false, string footer = "")
    {
        return to + subject + urgent + footer;
    }

    public void Send()
    {
        Compose("Hello", "a@example.com");
        Compose(subject: "Hi", to: "b@example.com");
        Compose("Report", "c@example.com", footer: "Thanks");
        Compose("Memo", "d@example.com", urgent: true);
    }
}
