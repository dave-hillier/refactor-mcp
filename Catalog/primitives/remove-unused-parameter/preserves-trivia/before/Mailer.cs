namespace Shop;

public class Mailer
{
    public string Send(
        string to,
        string body, // plain text
        bool legacy)
    {
        return to + ": " + body;
    }

    public string Welcome()
    {
        // greet a new customer
        return Send(
            "ann@example.com",
            "Welcome", // body
            false);
    }
}
