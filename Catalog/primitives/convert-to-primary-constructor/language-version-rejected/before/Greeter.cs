namespace Shop;

public class Greeter
{
    private readonly string _greeting;

    public Greeter(string greeting)
    {
        _greeting = greeting;
    }

    public string Greet(string name) => _greeting + ", " + name;
}
