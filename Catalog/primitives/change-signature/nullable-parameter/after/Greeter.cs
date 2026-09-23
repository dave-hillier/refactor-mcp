namespace Shop;

public class Greeter
{
    public string Greet(string name, string? title)
    {
        return "Hello " + name;
    }

    public string Welcome(string? nickname)
    {
        return Greet(nickname ?? "friend", null);
    }
}
