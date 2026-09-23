namespace Shop;

public class Mailer
{
    public string Compose(string to, string subject = "")
    {
        return to + subject;
    }
}
