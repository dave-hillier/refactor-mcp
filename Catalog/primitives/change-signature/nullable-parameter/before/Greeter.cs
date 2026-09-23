namespace Shop;

public class Greeter
{
    public string Greet(string name)
    {
        return "Hello " + name;
    }

    public string Welcome(string? nickname)
    {
        return Greet(nickname ?? "friend");
    }
}
