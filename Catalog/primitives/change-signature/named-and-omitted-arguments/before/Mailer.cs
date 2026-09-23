namespace Shop;

public class Mailer
{
    public string Compose(string to, string subject, bool urgent = false, string footer = "")
    {
        return to + subject + urgent + footer;
    }

    public void Send()
    {
        Compose("a@example.com", "Hello");
        Compose(subject: "Hi", to: "b@example.com");
        Compose("c@example.com", "Report", footer: "Thanks");
        Compose("d@example.com", "Memo", true);
    }
}
